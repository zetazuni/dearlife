import bpy, os, math
from mathutils import Vector

SRC = r"S:\Dearlife by Zetazuni\Assets\Art\Models"
OUT = r"S:\Dearlife by Zetazuni\Assets\Resources\Thumbs"
os.makedirs(OUT, exist_ok=True)

IDS = ("sofa beanbag marbletable geomrug tvunit uplight bookcase candles globe diningtable diningchair barstool longdining fridge espresso "
       "waterdispenser fruitbowl platformbed platformbed_e nightstand wardrobe bedlamp bathtub vanity towelrack washer dryer basket officedesk "
       "officechair teacherdesk filecabinet printer3d robotarm telescope treadmill weightbench spinbike punchbag yogamat dumbbells gardenbench "
       "lounger hammock parasol bbq bbqcounter firepit lantern outdoorsectional cooler planter planterbox flowerbed gnome flamingo mailbox "
       "wheelbarrow wateringcan hosereel workbench toolchest bicycle beachball").split()

if "ONLY" in globals():
    IDS = list(ONLY)
try:
    START, END
except NameError:
    START, END = 0, len(IDS)

scene = bpy.context.scene
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
    try:
        scene.render.engine = eng
        break
    except TypeError:
        pass
scene.render.film_transparent = True
scene.render.resolution_x = 256
scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
try:
    scene.eevee.taa_render_samples = 24
except Exception:
    pass
try:
    scene.view_settings.view_transform = "Standard"
except Exception:
    pass

# a soft world, one key light and a fill
if scene.world is None:
    scene.world = bpy.data.worlds.new("thumbWorld")
scene.world.use_nodes = True
bg = next((n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND"), None)
if bg:
    bg.inputs[0].default_value = (0.85, 0.85, 0.9, 1)
    bg.inputs[1].default_value = 0.45

made = []
for o in bpy.data.objects:
    if not o.name.startswith('thumb'):
        o.hide_render = True
        o.hide_viewport = True


def clear(names):
    for o in list(bpy.data.objects):
        if o.name in names:
            bpy.data.objects.remove(o, do_unlink=True)


cam_data = bpy.data.cameras.new("thumbCam")
cam_data.type = "ORTHO"
cam = bpy.data.objects.new("thumbCam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
sun_data = bpy.data.lights.new("thumbSun", "SUN")
sun_data.energy = 2.2
sun = bpy.data.objects.new("thumbSun", sun_data)
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
scene.collection.objects.link(sun)
fill_data = bpy.data.lights.new("thumbFill", "SUN")
fill_data.energy = 0.5
fill = bpy.data.objects.new("thumbFill", fill_data)
fill.rotation_euler = (math.radians(60), math.radians(0), math.radians(140))
scene.collection.objects.link(fill)

log = []
for i in range(START, min(END, len(IDS))):
    name = IDS[i]
    fname = name[6:] if name.startswith("adopt_") else name
    path = os.path.join(SRC, fname + ".fbx")
    if not os.path.exists(path):
        path = os.path.join(SRC, "PolyHaven", fname, fname + ".gltf")
    if not os.path.exists(path):
        path = os.path.join(SRC, "Cars", fname + ".glb")
    if not os.path.exists(path):
        log.append("missing " + name)
        continue
    before = set(bpy.data.objects.keys())
    try:
        import io as _io, contextlib as _cl
        with _cl.redirect_stdout(_io.StringIO()), _cl.redirect_stderr(_io.StringIO()):
            if path.endswith(".fbx"):
                if hasattr(bpy.ops.wm, "fbx_import"):
                    bpy.ops.wm.fbx_import(filepath=path)
                else:
                    bpy.ops.import_scene.fbx(filepath=path)
            else:
                bpy.ops.import_scene.gltf(filepath=path)
    except Exception as ex:
        log.append("import fail %s %s" % (name, ex))
        continue
    new = [bpy.data.objects[k] for k in bpy.data.objects.keys() if k not in before]
    if name == "sofa":
        for nm in [o.name for o in new]:
            if nm.startswith("seat cushion") and nm in bpy.data.objects:
                bpy.data.objects.remove(bpy.data.objects[nm], do_unlink=True)
        new = [bpy.data.objects[nm] for nm in [k for k in bpy.data.objects.keys() if k not in before]]
    meshes = [o for o in new if o.type == "MESH"]
    if not meshes:
        log.append("no mesh " + name)
        clear({o.name for o in new})
        continue
    bpy.context.view_layer.update()
    mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
    centre = (mn + mx) / 2
    size = mx - mn
    diag = size.length
    direction = Vector((0.85, -1.25, 0.95)).normalized()
    cam.location = centre + direction * (diag * 3 + 5)
    look = (centre - cam.location).to_track_quat("-Z", "Y")
    cam.rotation_euler = look.to_euler()
    cam_data.ortho_scale = max(0.6, diag * 0.92)
    cam_data.clip_start = 0.01
    cam_data.clip_end = 500
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    made.append(name)
    clear({o.name for o in new})

RESULT = "made %d: %s | %s" % (len(made), ",".join(made), "; ".join(log))
print(RESULT)
