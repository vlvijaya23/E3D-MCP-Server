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
using System.Reflection;
using E3DBridge.Staad;

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
    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "message", "E3D bridge running" },
            { "version", "SPINE_TEST_V1" }
        });

        case "/current":
            return Describe(CurrentElement.Element);

        case "/attributes":
            return Attributes(Resolve(job.Query["name"]));

        case "/children":
            return Children(Resolve(job.Query["name"]));

        case "/pingwrite":
            return PingWrite();

        case "/createsite":
            return CreateSite(job.Query["name"]);

        case "/createzone":
            return CreateZone(
                job.Query["site"],
                job.Query["name"]);

        case "/createstructure":
            return CreateStructure(
                job.Query["zone"],
                job.Query["name"]);

        case "/createbeam":
            return CreateBeam(
                job.Query["name"]);

        case "/createcolumn":
            return CreateColumn(
                job.Query["name"]);

        case "/createbrace":
            return CreateBrace(
                job.Query["name"]);

        case "/methods":
            return ListDbElementMethods();

        case "/currenttype":
            return CurrentType();

        case "/ownerinfo":
            return OwnerInfo();

        case "/createtestgensec":
            return CreateTestGensec();

        case "/createmethods":
            return ListCreateMethods();

        case "/spinetest":
            return CreateSpineTest();

        case "/createspine":
            return CreateSpineUnderCurrent();

        case "/createjldatu":
            return CreateJldatu();

        case "/jldatuattrs":
            return JldatuAttributes();

        case "/setjldatu":
            return SetJldatuTest();

        case "/zdistype":
            return GetZdisType();

        case "/readjldatu":
            return ReadJldatuValues();

        case "/dumpchildren":
            return DumpChildren();

        case "/attrtype":
            return AttributeTypes();

        case "/setattrmethods":
            return SetAttributeMethods();

        case "/readpoinsp":
            return ReadPoinsp();

        case "/readpos":
            return ReadPos();

        case "/readpldatu":
            return ReadPldatu();

        case "/readfixing":
            return ReadFixing();

        case "/readgensec":
            return ReadGensec();

        case "/readbeamcore":
            return ReadBeamCore();

        case "/readgeneratedbeam":
            return ReadGeneratedBeam();

        case "/DumpJldatuChildren":
            return DumpJldatuChildren();

        case "/readjois":
            return ReadAttr("JOIS");

        case "/readjoie":
            return ReadAttr("JOIE");

        case "/readpoint":
            return ReadPoint();

        case "/positionctors":
            return PositionConstructors();

        case "/positionmethods":
            return PositionMethods();

        case "/readstaad":
            return ReadStaad();

case "/version":
    return Version();

case "/readstaadmodel":
    return ReadStaadModel();

case "/readstaadmembers":
    return ReadStaadMembers();

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

private string PingWrite()
{
    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "message", "Write endpoint working" }
        });
}

private string CreateSite(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Error("Site name required");

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "action", "createsite" },
            { "name", name }
        });
}

private string CreateZone(string site, string name)
{
    if (string.IsNullOrWhiteSpace(site))
        return Error("Site required");

    if (string.IsNullOrWhiteSpace(name))
        return Error("Zone name required");

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "action", "createzone" },
            { "site", site },
            { "name", name }
        });
}

private string CreateStructure(string zone, string name)
{
    if (string.IsNullOrWhiteSpace(zone))
        return Error("Zone required");

    if (string.IsNullOrWhiteSpace(name))
        return Error("Structure name required");

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "action", "createstructure" },
            { "zone", zone },
            { "name", name }
        });
}

private string CreateBeam(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Error("Beam name required");

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "action", "createbeam" },
            { "name", name }
        });
}

        private string CreateColumn(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Error("Column name required");

            return _json.Serialize(
                new Dictionary<string, object>
                {
                    { "ok", true },
                    { "action", "createcolumn" },
                    { "name", name }
                });
        }

        private string CreateBrace(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Error("Brace name required");

            return _json.Serialize(
                new Dictionary<string, object>
                {
                    { "ok", true },
                    { "action", "createbrace" },
                    { "name", name }
                });
        }

private string ListDbElementMethods()
{
    MethodInfo[] methods =
        typeof(DbElement).GetMethods();

    List<string> names =
        new List<string>();

    foreach (MethodInfo method in methods)
    {
        names.Add(method.Name);
    }

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "methods", names }
        });
}

private string ListCreateMethods()
{
    MethodInfo[] methods =
        typeof(DbElement).GetMethods();

    List<string> names =
        new List<string>();

    foreach (MethodInfo method in methods)
    {
        if (method.Name.StartsWith("Create"))
        {
            names.Add(method.ToString());
        }
    }

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "methods", names }
        });
}

private string ListElementTypes()
{
    return _json.Serialize(
        new Dictionary<string, object>
        {
            {
                "types",
                Enum.GetNames(typeof(DbElementType))
            }
        });
}

private string CurrentType()
{
    DbElement ce = CurrentElement.Element;

    object elementType = ce.ElementType;

    Type t = elementType.GetType();

    List<string> members = new List<string>();

    foreach (MemberInfo m in t.GetMembers())
    {
        members.Add(m.MemberType + ": " + m.Name);
    }

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "elementType", elementType.ToString() },
            { "typeClass", t.FullName },
            { "members", members }
        });
}

private string OwnerInfo()
{
    DbElement ce = CurrentElement.Element;

    DbElement owner = ce.Owner;

    return _json.Serialize(
        new Dictionary<string, object>
        {
            { "ok", true },
            { "ownerName", SafeGet(owner, DbAttributeInstance.FLNM) },
            { "ownerType", SafeGet(owner, DbAttributeInstance.TYPE) }
        });
}

private string CreateTestGensec()
{
    try
    {
        DbElement framework =
            DbElement.GetElement("/FRMW/SANI/Elev_1");

        DbElementType gensecType =
            DbElementType.GetElementType("GENSEC");

        DbElement member =
            framework.CreateLast(gensecType);

        member.SetAttribute(
            DbAttribute.GetDbAttribute("GTYP"),
            "PFI");

        member.SetAttribute(
            DbAttribute.GetDbAttribute("STRUCL"),
            "BEAM");

        DbElement profile =
            DbElement.GetElement("/EU-HE160A");

        member.SetAttribute(
            DbAttribute.GetDbAttribute("SPRE"),
            profile);

DbElementType spineType =
    DbElementType.GetElementType("SPINE");

DbElement spine =
    member.CreateLast(spineType);

DbElementType poinspType =
    DbElementType.GetElementType("POINSP");

DbElement poinsp1 =
    spine.CreateLast(poinspType);

DbElement poinsp2 =
    spine.CreateLast(poinspType);

Aveva.Core.Geometry.Position p1 =
    Aveva.Core.Geometry.Position.Create(
        0.0,
        0.0,
        0.0);

Aveva.Core.Geometry.Position p2 =
    Aveva.Core.Geometry.Position.Create(
        2640.0,
        0.0,
        0.0);

poinsp1.SetAttribute(
    DbAttribute.GetDbAttribute("POS"),
    p1);

poinsp2.SetAttribute(
    DbAttribute.GetDbAttribute("POS"),
    p2);

DbElementType jldatuType =
    DbElementType.GetElementType("JLDATU");

DbElement jldatu1 =
    member.CreateLast(jldatuType);

jldatu1.SetAttribute(
    DbAttribute.GetDbAttribute("ZDIS"),
    0.0);

DbElement jldatu2 =
    member.CreateLast(jldatuType);

jldatu2.SetAttribute(
    DbAttribute.GetDbAttribute("ZDIS"),
    0.0);

DbElement jldatu3 =
    member.CreateLast(jldatuType);

jldatu3.SetAttribute(
    DbAttribute.GetDbAttribute("ZDIS"),
    0.0);

DbElement jldatu4 =
    member.CreateLast(jldatuType);

jldatu4.SetAttribute(
    DbAttribute.GetDbAttribute("ZDIS"),
    2095.0);

DbElement jldatu5 =
    member.CreateLast(jldatuType);

jldatu5.SetAttribute(
    DbAttribute.GetDbAttribute("ZDIS"),
    2560.0);

        List<string> children =
            new List<string>();

        foreach (DbElement child in member.Members())
        {
            children.Add(
                SafeGet(child, DbAttributeInstance.TYPE));
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "member", SafeGet(member, DbAttributeInstance.FLNM) },
                { "children", children }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string CreateSpineUnderCurrent()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        DbElementType spineType =
            DbElementType.GetElementType("SPINE");

        DbElement spine =
            ce.CreateLast(spineType);

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "spine", SafeGet(spine, DbAttributeInstance.FLNM) },
                { "type", SafeGet(spine, DbAttributeInstance.TYPE) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string CreateJldatu()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        DbElementType jldatuType =
            DbElementType.GetElementType("JLDATU");

        DbElement jldatu =
            ce.CreateLast(jldatuType);

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "name", SafeGet(jldatu, DbAttributeInstance.FLNM) },
                { "type", SafeGet(jldatu, DbAttributeInstance.TYPE) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string JldatuAttributes()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        List<string> attrs =
            new List<string>();

        foreach (DbAttribute a in ce.GetAttributes())
        {
            attrs.Add(a.Name);
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "type", SafeGet(ce, DbAttributeInstance.TYPE) },
                { "attributes", attrs }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadJldatuValues()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "type", SafeGet(ce, DbAttributeInstance.TYPE) },
                { "ZDIS", SafeGet(ce, DbAttribute.GetDbAttribute("ZDIS")) },
                { "PKDI", SafeGet(ce, DbAttribute.GetDbAttribute("PKDI")) },
                { "LEVE", SafeGet(ce, DbAttribute.GetDbAttribute("LEVE")) },
                { "FIXT", SafeGet(ce, DbAttribute.GetDbAttribute("FIXT")) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadGensec()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "GTYP", SafeGet(ce, DbAttribute.GetDbAttribute("GTYP")) },
                { "STRUCL", SafeGet(ce, DbAttribute.GetDbAttribute("STRUCL")) },
                { "POS", SafeGet(ce, DbAttribute.GetDbAttribute("POS")) },
                { "ORI", SafeGet(ce, DbAttribute.GetDbAttribute("ORI")) },
                { "SPRE", SafeGet(ce, DbAttribute.GetDbAttribute("SPRE")) },
                { "JOIS", SafeGet(ce, DbAttribute.GetDbAttribute("JOIS")) },
                { "JOIE", SafeGet(ce, DbAttribute.GetDbAttribute("JOIE")) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadPoint()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "NAME", SafeGet(ce, DbAttribute.GetDbAttribute("NAME")) },
                { "POS", SafeGet(ce, DbAttribute.GetDbAttribute("POS")) },
                { "ORI", SafeGet(ce, DbAttribute.GetDbAttribute("ORI")) }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadAttr(string attrName)
{
    try
    {
        DbElement ce = CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "attribute", attrName },
                { "value",
                    SafeGet(
                        ce,
                        DbAttribute.GetDbAttribute(attrName))
                }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string SetJldatuTest()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        ce.SetAttribute(
            DbAttribute.GetDbAttribute("ZDIS"),
            1000.0);

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "ZDIS",
                    SafeGet(
                        ce,
                        DbAttribute.GetDbAttribute("ZDIS"))
                }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string DumpChildren()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        List<object> rows =
            new List<object>();

        foreach (DbElement child in ce.Members())
        {
            rows.Add(
                new Dictionary<string, object>
                {
                    { "name", SafeGet(child, DbAttributeInstance.FLNM) },
                    { "type", SafeGet(child, DbAttributeInstance.TYPE) },

                    { "ZDIS", SafeGet(child,
                        DbAttribute.GetDbAttribute("ZDIS")) },

                    { "PKDI", SafeGet(child,
                        DbAttribute.GetDbAttribute("PKDI")) },

                    { "LEVE", SafeGet(child,
                        DbAttribute.GetDbAttribute("LEVE")) },

                    { "FIXT", SafeGet(child,
                        DbAttribute.GetDbAttribute("FIXT")) }
                });
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "children", rows }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string CreateSpineTest()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        List<string> methods =
            new List<string>();

        foreach (MethodInfo m in ce.GetType().GetMethods())
        {
            methods.Add(m.ToString());
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "ce", SafeGet(ce, DbAttributeInstance.FLNM) },
                { "type", SafeGet(ce, DbAttributeInstance.TYPE) },
                { "methods", methods }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string AttributeTypes()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        DbAttribute zdis =
            DbAttribute.GetDbAttribute("ZDIS");

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "attribute", zdis.Name },
                { "class", zdis.GetType().FullName }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string SetAttributeMethods()
{
    try
    {
        List<string> methods =
            new List<string>();

        foreach(MethodInfo m in typeof(DbElement).GetMethods())
        {
            if(m.Name == "SetAttribute")
            {
                methods.Add(m.ToString());
            }
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "methods", methods }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string GetZdisType()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        object value =
            ce.GetAttribute(
                DbAttribute.GetDbAttribute("ZDIS"));

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "dotnetType", value.GetType().FullName },
                { "value", value.ToString() }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadPoinsp()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "type", SafeGet(ce, DbAttributeInstance.TYPE) },
                { "POS", SafeGet(ce, DbAttribute.GetDbAttribute("POS")) },
                { "ORRF", SafeGet(ce, DbAttribute.GetDbAttribute("ORRF")) },
                { "LNFA", SafeGet(ce, DbAttribute.GetDbAttribute("LNFA")) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadPos()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "type", SafeGet(ce, DbAttributeInstance.TYPE) },
                { "POS", SafeGet(ce, DbAttribute.GetDbAttribute("POS")) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadPldatu()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "POSL", SafeGet(ce, DbAttribute.GetDbAttribute("POSL")) },
                { "YDIR", SafeGet(ce, DbAttribute.GetDbAttribute("YDIR")) },
                { "LEVE", SafeGet(ce, DbAttribute.GetDbAttribute("LEVE")) },
                { "ORRF", SafeGet(ce, DbAttribute.GetDbAttribute("ORRF")) }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadFixing()
{
    try
    {
        DbElement ce =
            CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },

                { "POS", SafeGet(ce,
                    DbAttribute.GetDbAttribute("POS")) },

                { "ORI", SafeGet(ce,
                    DbAttribute.GetDbAttribute("ORI")) },

                { "SPRE", SafeGet(ce,
                    DbAttribute.GetDbAttribute("SPRE")) },

                { "MFIXREF", SafeGet(ce,
                    DbAttribute.GetDbAttribute("MFIXREF")) },

                { "CREF", SafeGet(ce,
                    DbAttribute.GetDbAttribute("CREF")) },

                { "CFRA", SafeGet(ce,
                    DbAttribute.GetDbAttribute("CFRA")) }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadBeamCore()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "POS", SafeGet(ce, DbAttribute.GetDbAttribute("POS")) },
                { "ORI", SafeGet(ce, DbAttribute.GetDbAttribute("ORI")) },
                { "JOIS", SafeGet(ce, DbAttribute.GetDbAttribute("JOIS")) },
                { "JOIE", SafeGet(ce, DbAttribute.GetDbAttribute("JOIE")) },
                { "JLNS", SafeGet(ce, DbAttribute.GetDbAttribute("JLNS")) },
                { "JLNE", SafeGet(ce, DbAttribute.GetDbAttribute("JLNE")) }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadGeneratedBeam()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "POS", SafeGet(ce, DbAttribute.GetDbAttribute("POS")) },
                { "ORI", SafeGet(ce, DbAttribute.GetDbAttribute("ORI")) },
                { "JOIS", SafeGet(ce, DbAttribute.GetDbAttribute("JOIS")) },
                { "JOIE", SafeGet(ce, DbAttribute.GetDbAttribute("JOIE")) },
                { "SPRE", SafeGet(ce, DbAttribute.GetDbAttribute("SPRE")) }
            });
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string DumpJldatuChildren()
{
    try
    {
        DbElement ce = CurrentElement.Element;

        List<object> rows =
            new List<object>();

        foreach (DbElement child in ce.Members())
        {
            rows.Add(
                new Dictionary<string, object>
                {
                    { "name", SafeGet(child, DbAttributeInstance.FLNM) },
                    { "type", SafeGet(child, DbAttributeInstance.TYPE) }
                });
        }

        return _json.Serialize(rows);
    }
    catch(Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string PositionConstructors()
{
    try
    {
        List<string> ctors =
            new List<string>();

        foreach (ConstructorInfo c in
            typeof(Aveva.Core.Geometry.Position)
            .GetConstructors())
        {
            ctors.Add(c.ToString());
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "constructors", ctors }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string PositionMethods()
{
    try
    {
        List<string> methods =
            new List<string>();

        foreach (MethodInfo m in
            typeof(Aveva.Core.Geometry.Position)
            .GetMethods())
        {
            methods.Add(m.ToString());
        }

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "methods", methods }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadStaad()
{
    try
    {
        string text =
            StaadParser.ReadFile(
                @"C:\e3d-mcp\sample.std");

        return _json.Serialize(
            new Dictionary<string, object>
            {
                { "ok", true },
                { "content", text }
            });
    }
    catch (Exception ex)
    {
        return Error(ex.ToString());
    }
}

private string ReadStaadMembers()
{
    try
    {
        var members =
            StaadParser.GetMembers(
                @"C:\E3D-mcp\sample.std");

        return "Members found = " +
               members.Count;
    }
    catch(Exception ex)
    {
        return ex.ToString();
    }
}

private string Version()
{
    return "STAAD_IMPORT_V1";
}

private string ReadStaadModel()
{
    var nodes =
        StaadParser.GetNodes(
            @"C:\E3D-mcp\sample.std");

    return nodes[nodes.Count - 1].ToString();
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