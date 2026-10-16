"""
AVEVA E3D MCP server (starter, read-only)

Talks to the E3DBridge add-in running inside E3D (http://localhost:8765).
Needs only:  pip install "mcp[cli]<2"
"""
import json
import os
import urllib.error
import urllib.parse
import urllib.request

from mcp.server.fastmcp import FastMCP

BRIDGE = os.environ.get("E3D_BRIDGE_URL", "http://localhost:8765")

mcp = FastMCP("e3d")


def _call(path: str, **params) -> dict:
    query = urllib.parse.urlencode({k: v for k, v in params.items() if v})
    url = f"{BRIDGE}{path}" + (f"?{query}" if query else "")
    try:
        with urllib.request.urlopen(url, timeout=30) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.URLError:
        return {
            "ok": False,
            "error": (
                "Cannot reach the E3D bridge. Make sure E3D is open and the bridge "
                "was started (import 'E3DBridge', then Start())."
            ),
        }
    except Exception as exc:  # unexpected response format etc.
        return {"ok": False, "error": str(exc)}


@mcp.tool()
def bridge_status() -> dict:
    """Check that the connection to AVEVA E3D is working."""
    return _call("/status")


@mcp.tool()
def get_current_element() -> dict:
    """Return the name and type of the element currently selected (CE) in E3D."""
    return _call("/current")


@mcp.tool()
def get_element_attributes(name: str = "") -> dict:
    """Return all attributes and values of an element.
    `name` is the element name with a leading slash (e.g. '/VESS1').
    Leave empty to use the current element."""
    return _call("/attributes", name=name)


@mcp.tool()
def get_children(name: str = "") -> dict:
    """List the direct members (children) of an element, e.g. the zones inside
    a site. `name` has a leading slash (e.g. '/SITE1'). Leave empty for the
    current element. Returns at most 300 items."""
    return _call("/children", name=name)


if __name__ == "__main__":
    mcp.run()
