// E3DBridge.cs
// Runs INSIDE AVEVA E3D (loaded with PML.NET). Starts a small local web listener
// (localhost:8765) so the Python MCP server can ask read-only questions.
//
// All database access is done on E3D's main (UI) thread through a timer,
// because the E3D database API must not be called from background threads.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Aveva.ApplicationFramework;
using Aveva.Core.Database;
using Aveva.Core.PMLNet;

[assembly: PMLNetCallable()]

namespace E3DBridge
{
    [PMLNetCallable()]
    public class BridgeServer
    {
        private const string Prefix = "http://localhost:8765/";
        private const int MaxItems = 300;

        private class Job
        {
            public string Path;
            public NameValueCollection Query;
            public string Result;
            public ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private readonly ConcurrentQueue<Job> _queue = new ConcurrentQueue<Job>();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private HttpListener _listener;
        private Thread _thread;
        private System.Windows.Forms.Timer _timer;
        private volatile bool _running;

        [PMLNetCallable()]
        public BridgeServer() { }

        // PML.NET requires an Assign method on every callable class.
        [PMLNetCallable()]
        public void Assign(BridgeServer that) { }

        // Call this from the E3D Command Window (on the main thread).
        [PMLNetCallable()]
        public void StartBridge()
        {
            if (_running) return;

            // Timer must be created on the UI thread so it ticks on the UI thread.
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 100;
            _timer.Tick += (s, e) => ProcessQueue();
            _timer.Start();

            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);
            _listener.Start();

            _running = true;
            _thread = new Thread(ListenLoop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        [PMLNetCallable()]
        public void StopBridge()
        {
            _running = false;
            try { if (_timer != null) _timer.Stop(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
        }

        [PMLNetCallable()]
        public string BridgeStatus()
        {
            return _running ? "E3D bridge running on " + Prefix : "E3D bridge stopped";
        }

        // ---------------- background thread: HTTP only, no E3D calls ----------------
        private void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; }

                try
                {
                    Job job = new Job();
                    job.Path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();
                    job.Query = HttpUtility.ParseQueryString(ctx.Request.Url.Query);
                    _queue.Enqueue(job);

                    if (!job.Done.Wait(20000))
                        job.Result = Error("Timed out waiting for E3D (is it busy or showing a dialog?)");

                    Write(ctx, job.Result);
                }
                catch (Exception ex)
                {
                    try { Write(ctx, Error(ex.Message)); } catch { }
                }
            }
        }

        private static void Write(HttpListenerContext ctx, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? "{}");
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        // ---------------- UI thread: all E3D database access happens here ----------------
        private void ProcessQueue()
        {
            Job job;
            int n = 0;
            while (n++ < 5 && _queue.TryDequeue(out job))
            {
                try { job.Result = Handle(job); }
                catch (Exception ex) { job.Result = Error(ex.Message); }
                job.Done.Set();
            }
        }

        private string Handle(Job job)
        {
            switch (job.Path)
            {
                case "/status":
                    return _json.Serialize(new Dictionary<string, object> {
                        { "ok", true }, { "message", "E3D bridge running" } });

                case "/current":
                    return Describe(CurrentElement.Element);

                case "/attributes":
                    return Attributes(Resolve(job.Query["name"]));

                case "/children":
                    return Children(Resolve(job.Query["name"]));

                default:
                    return Error("Unknown path: " + job.Path);
            }
        }

        // Empty name = current element. Names need a leading slash, e.g. /VESS1
        private static DbElement Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return CurrentElement.Element;
            name = name.Trim();
            if (!name.StartsWith("/") && !name.StartsWith("=")) name = "/" + name;
            return DbElement.GetElement(name);
        }

        private static string SafeGet(DbElement e, DbAttribute a)
        {
            try { return e.GetAsString(a); }
            catch { return null; }
        }

        private string Describe(DbElement e)
        {
            string full = SafeGet(e, DbAttributeInstance.FLNM);
            if (full == null) return Error("No valid element found");
            return _json.Serialize(new Dictionary<string, object> {
                { "ok", true },
                { "name", full },
                { "type", SafeGet(e, DbAttributeInstance.TYPE) } });
        }

        private string Attributes(DbElement e)
        {
            if (SafeGet(e, DbAttributeInstance.FLNM) == null)
                return Error("Element not found. Names need a leading slash, e.g. /VESS1");

            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            foreach (DbAttribute a in e.GetAttributes())
            {
                if (list.Count >= MaxItems) break;
                string v = SafeGet(e, a);
                if (v == null) continue;
                list.Add(new Dictionary<string, object> { { "attribute", a.Name }, { "value", v } });
            }
            return _json.Serialize(new Dictionary<string, object> {
                { "ok", true },
                { "name", SafeGet(e, DbAttributeInstance.FLNM) },
                { "attributes", list } });
        }

        private string Children(DbElement e)
        {
            if (SafeGet(e, DbAttributeInstance.FLNM) == null)
                return Error("Element not found. Names need a leading slash, e.g. /SITE1");

            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            foreach (DbElement m in e.Members())
            {
                if (list.Count >= MaxItems) break;
                list.Add(new Dictionary<string, object> {
                    { "name", SafeGet(m, DbAttributeInstance.FLNM) },
                    { "type", SafeGet(m, DbAttributeInstance.TYPE) } });
            }
            return _json.Serialize(new Dictionary<string, object> {
                { "ok", true },
                { "parent", SafeGet(e, DbAttributeInstance.FLNM) },
                { "count", list.Count },
                { "children", list } });
        }

        private string Error(string message)
        {
            return _json.Serialize(new Dictionary<string, object> {
                { "ok", false }, { "error", message } });
        }
    }

    // Auto-start: E3D loads this class at startup when "E3DBridge" is listed in
    // DesignAddins.xml, so nobody has to type the PML commands any more.
    public class BridgeAddin : IAddin
    {
        private BridgeServer _server;

        public string Name
        {
            get { return "E3DBridge"; }
        }

        public string Description
        {
            get { return "Local read-only bridge for the Claude MCP server"; }
        }

        public void Start(ServiceManager serviceManager)
        {
            try
            {
                _server = new BridgeServer();
                _server.StartBridge();
            }
            catch (Exception)
            {
                // Never stop E3D from starting because of the bridge.
            }
        }

        public void Stop()
        {
            try { if (_server != null) _server.StopBridge(); }
            catch (Exception) { }
        }
    }
}
