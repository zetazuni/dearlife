"""Painting textures by 3D position (shared by blender_pets.py and blender_skin_layers.py).

texel_positions fills a mesh's UV layout with the 3D position of every texel, so a texture can be painted with rules in
3D ("patches on the back", "lipstick round the mouth") instead of by hand in UV space.
"""
import numpy as np


def texel_positions(mesh, size, matrix=None, uv_name=None, faces=False):
    """For every texel covered by the mesh's UV layout: the 3D position there (NaN where nothing is). World space when
    `matrix` (the object's matrix_world) is given, else the mesh's own space. With faces=True it also returns, per
    texel, the index of the polygon it lies on (-1 where nothing is)."""
    me = mesh.data
    me.calc_loop_triangles()
    uv = (me.uv_layers[uv_name] if uv_name else me.uv_layers.active).data
    P = np.full((size, size, 3), np.nan, np.float32)
    F = np.full((size, size), -1, np.int32) if faces else None
    co = np.array([v.co[:] for v in me.vertices], np.float32)
    if matrix is not None:
        m = np.array(matrix, np.float32)
        co = co @ m[:3, :3].T + m[:3, 3]
    for t in me.loop_triangles:
        uvs = np.array([uv[l].uv[:] for l in t.loops], np.float32) * size
        ps = co[list(t.vertices)]
        x0, y0 = np.floor(uvs.min(0) - 1).astype(int)
        x1, y1 = np.ceil(uvs.max(0) + 1).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a, b, c = uvs
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-9:
            continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / d
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= -0.03) & (w1 >= -0.03) & (w2 >= -0.03)   # a little over the edge, so seams do not show
        if not inside.any():
            continue
        pos = w0[..., None] * ps[0] + w1[..., None] * ps[1] + w2[..., None] * ps[2]
        sub = P[y0:y1 + 1, x0:x1 + 1]
        empty = np.isnan(sub[..., 0]) & inside
        sub[empty] = pos[empty]
        if faces:
            F[y0:y1 + 1, x0:x1 + 1][empty] = t.polygon_index
    return (P, F) if faces else P


def uv_islands(mesh, uv_name=None):
    """An island number for every polygon: polygons are in one island when they share an edge with the same UVs."""
    me = mesh.data
    uv = (me.uv_layers[uv_name] if uv_name else me.uv_layers.active).data
    edges = {}
    for p in me.polygons:
        n = len(p.loop_indices)
        for k in range(n):
            a, b = p.loop_indices[k], p.loop_indices[(k + 1) % n]
            va, vb = me.loops[a].vertex_index, me.loops[b].vertex_index
            ua, ub = tuple(round(c, 5) for c in uv[a].uv), tuple(round(c, 5) for c in uv[b].uv)
            key = (min(va, vb), max(va, vb), min(ua, ub), max(ua, ub))
            edges.setdefault(key, []).append(p.index)
    parent = list(range(len(me.polygons)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for ps in edges.values():
        for q in ps[1:]:
            ra, rb = find(ps[0]), find(q)
            if ra != rb:
                parent[ra] = rb
    return np.array([find(i) for i in range(len(me.polygons))], np.int32)


def box_blur(a, r):
    k = 2 * r + 1
    p = np.pad(a, ((r + 1, r), (r + 1, r)), mode='edge')
    c = p.cumsum(0).cumsum(1)
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)
