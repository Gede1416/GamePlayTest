# 场景一致性检查：fileID 引用、组件归属、根节点、缩进。一次性脚本，用完可删。
import re, sys, collections

SCENE = r"Assets/Scenes/SampleScene.unity"
text = open(SCENE, encoding="utf-8").read()
lines = text.splitlines()
errs, warns = [], []

if "\t" in text:
    errs.append("文件里有 Tab（Unity YAML 只用空格）")
if "\r" in text:
    errs.append("文件里有 CR（应该是 LF）")

# --- 块头
blocks = []          # (type, id, 起始行)
for i, ln in enumerate(lines):
    m = re.match(r"^--- !u!(\d+) &(\d+)(?: stripped)?$", ln)
    if m:
        blocks.append((m.group(1), m.group(2), i + 1))
    elif ln.startswith("---"):
        errs.append(f"第 {i+1} 行块头格式不对: {ln}")

ids = [b[1] for b in blocks]
dups = [k for k, v in collections.Counter(ids).items() if v > 1]
if dups:
    errs.append(f"重复的 fileID: {dups}")

defined = set(ids)

# --- 局部引用必须能解析
for i, ln in enumerate(lines):
    for ref in re.findall(r"\{fileID: (\d+)\}", ln):
        if ref != "0" and ref not in defined:
            errs.append(f"第 {i+1} 行引用了不存在的 fileID {ref}")

# --- 缩进（块头除外）
for i, ln in enumerate(lines):
    if ln.startswith("---"):
        continue
    indent = len(ln) - len(ln.lstrip(" "))
    if indent % 2:
        errs.append(f"第 {i+1} 行缩进不是 2 的倍数: {ln!r}")

# --- GameObject <-> 组件 双向一致（只看块自己的行范围）
block_of = {b[1]: b for b in blocks}
bounds = {}
for k, (t, bid, start) in enumerate(blocks):
    end = min(blocks[k + 1][2], len(lines)) if k + 1 < len(blocks) else len(lines)
    bounds[bid] = (start, end)

go_owner = {}        # 组件 id -> GameObject id
for t, bid, start in blocks:
    if t == "1":
        continue
    for j in range(start, bounds[bid][1]):
        m = re.match(r"^  m_GameObject: \{fileID: (\d+)\}$", lines[j])
        if m and m.group(1) != "0":
            go_owner[bid] = m.group(1)
            break

for go_id, go_type, start in [(b[1], b[0], b[2]) for b in blocks if b[0] == "1"]:
    comps = []
    for j in range(start, bounds[go_id][1]):
        m = re.match(r"^  - component: \{fileID: (\d+)\}$", lines[j])
        if m:
            comps.append(m.group(1))
        if lines[j].startswith("  m_Layer:"):
            break
    for c in comps:
        if c not in defined:
            errs.append(f"GameObject {go_id} 列了不存在的组件 {c}")
        elif go_owner.get(c) != go_id:
            errs.append(f"GameObject {go_id} 列了组件 {c}，但该组件属于 {go_owner.get(c)}")
    owned = [c for c, o in go_owner.items() if o == go_id]
    for c in owned:
        if c not in comps:
            errs.append(f"组件 {c} 属于 GameObject {go_id}，但没列进它的 m_Component")

# --- SceneRoots
roots = re.search(r"SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n((?:  - \{fileID: \d+\}\n)+)", text)
if not roots:
    errs.append("找不到 SceneRoots.m_Roots")
else:
    for r in re.findall(r"\{fileID: (\d+)\}", roots.group(1)):
        if r not in defined:
            errs.append(f"SceneRoots 里的 {r} 不存在")
        else:
            kind = block_of[r][0]
            if kind not in ("4", "224"):
                errs.append(f"SceneRoots 里的 {r} 类型是 !u!{kind}，不是 Transform/RectTransform")
            else:
                father = re.search(rf"^--- !u!{kind} &{r}\n(?:.*\n)*?  m_Father: \{{fileID: (\d+)\}}",
                                   text, re.M)
                if not father or father.group(1) != "0":
                    errs.append(f"SceneRoots 里的 {r} 的 m_Father 不是 0")

# --- 父子关系双向一致（Transform/RectTransform）
parent_of, children_of = {}, {}
for bid, (start, end) in bounds.items():
    block = lines[start - 1:end]
    if not block or not re.match(r"^(Transform|RectTransform):$", block[0]):
        continue
    father = None
    kids = []
    in_kids = False
    for ln in block:
        m = re.match(r"^  m_Father: \{fileID: (\d+)\}$", ln)
        if m:
            father = m.group(1)
            in_kids = False
            continue
        if ln.startswith("  m_Children:"):
            in_kids = True
            continue
        if in_kids:
            m = re.match(r"^  - \{fileID: (\d+)\}$", ln)
            if m:
                kids.append(m.group(1))
            else:
                in_kids = False
    parent_of[bid] = father
    children_of[bid] = kids

for bid, kids in children_of.items():
    for k in kids:
        if k not in defined:
            errs.append(f"transform {bid} 的子节点 {k} 不存在")
        elif parent_of.get(k) != bid:
            errs.append(f"transform {bid} 把 {k} 列为子节点，但它的 m_Father 是 {parent_of.get(k)}")
for bid, f in parent_of.items():
    if f and f != "0" and bid not in children_of.get(f, []):
        errs.append(f"transform {bid} 的父节点是 {f}，但父节点没把它列为子节点")

print(f"块数 {len(blocks)}，行数 {len(lines)}")
if errs:
    print("错误:")
    for e in errs:
        print("  -", e)
else:
    print("OK: fileID 引用、组件归属、SceneRoots、缩进全部通过")
sys.exit(1 if errs else 0)
