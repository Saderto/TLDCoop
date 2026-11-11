# Extracts selected objects (and what they reference inside the same file) from WINTERMUTE ep5 bundles.
# Same logic as the per-mod ep5_extract scripts, as a function; bundles are loaded once and cached.
import UnityPy, os, json

_cache = {}
def load(b):
    if b not in _cache: _cache[b] = UnityPy.load(b)
    return _cache[b]

def refs(t, acc):
    if isinstance(t, dict):
        if "m_FileID" in t and "m_PathID" in t:
            acc.append((t["m_FileID"], t["m_PathID"]))
        else:
            for v in t.values(): refs(v, acc)
    elif isinstance(t, (list, tuple)):
        for v in t: refs(v, acc)

def run(bundles, WANT, out, P=print):
    os.makedirs(out, exist_ok=True)
    found = {}
    for b in bundles:
        env = load(b)
        for f in env.files.values():
            for name, sf in getattr(f, "files", {}).items():
                if name in WANT and hasattr(sf, "objects"):
                    found[name] = (b, env, sf)
                    P("found", name, "in", os.path.basename(b))
        if len(found) == len(WANT): break

    result = {}
    for cab, pids in WANT.items():
        if cab not in found:
            P("NOT FOUND", cab); continue
        b, env, sf = found[cab]
        cont = {o.path_id: p for p, o in env.container.items()}
        seen = set(); q = list(pids); items = []
        while q:
            pid = q.pop()
            if pid in seen: continue
            seen.add(pid)
            o = sf.objects.get(pid)
            if o is None: P("missing", cab, pid); continue
            try:
                tt = o.read_typetree()
            except Exception as e:
                tt = None; P("no typetree", pid, e)
            acc = []
            if tt is not None: refs(tt, acc)
            for fid, p in acc:
                if fid == 0 and p: q.append(p)
            ext = sorted({sf.externals[fid - 1].path.split("/")[-1] for fid, p in acc if fid > 0 and p})
            raw = o.get_raw_data()
            item = {"path_id": pid, "class_id": o.type.value, "type": o.type.name,
                    "name": (tt or {}).get("m_Name"), "container": cont.get(pid, ""),
                    "raw": f"{pid}.bin", "externals_used": ext}
            open(os.path.join(out, f"{pid}.bin"), "wb").write(bytes(raw))
            if tt is not None:
                json.dump(tt, open(os.path.join(out, f"{pid}.json"), "w", encoding="utf-8"), default=str, ensure_ascii=False)
            if o.type.name == "Texture2D":
                tex = o.read()
                try:
                    tex.image.save(os.path.join(out, f"{pid}.png")); item["png"] = f"{pid}.png"
                except Exception as e:
                    P("png fail", pid, e)
                # raw stream data (exact GPU format)
                sd = getattr(tex, "m_StreamData", None)
                try:
                    data = tex.get_image_data() if hasattr(tex, "get_image_data") else None
                except Exception:
                    data = None
                if data is None:
                    try:
                        data = tex.image_data
                    except Exception:
                        data = None
                if data:
                    open(os.path.join(out, f"{pid}.texdata"), "wb").write(bytes(data)); item["texdata"] = f"{pid}.texdata"
            if tt is not None and isinstance(tt.get("m_StreamData"), dict) and tt["m_StreamData"].get("path"):
                sd = tt["m_StreamData"]
                try:
                    rn = sd["path"].split("/")[-1]
                    bfile = list(env.files.values())[0]
                    r = bfile.files.get(rn)
                    blob = bytes(r.bytes if hasattr(r, "bytes") else r.read_bytes(r.Length))[sd["offset"]: sd["offset"] + sd["size"]]
                    open(os.path.join(out, f"{pid}.stream"), "wb").write(blob); item["stream"] = f"{pid}.stream"
                except Exception as e:
                    P("stream fail", pid, e)
            if o.type.name == "Mesh":
                try:
                    open(os.path.join(out, f"{pid}.obj"), "w").write(o.read().export()); item["obj"] = f"{pid}.obj"
                except Exception as e:
                    P("obj fail", pid, e)
            if o.type.name == "Shader":
                try:
                    item["shader_name"] = o.read().m_ParsedForm.m_Name
                except Exception:
                    pass
            items.append(item)
            P(cab, pid, item["type"], item["name"], item.get("shader_name", ""), item["container"], ext)
        result[cab] = {"bundle": os.path.basename(b), "externals": [e.path for e in sf.externals], "items": items}
    json.dump(result, open(os.path.join(out, "index.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    P("DONE")

    return result
