# FlickerVoid's Blender round trip: the editor starts Blender with this script (Open in Blender in a creator).
#
#   blender [Name.blend] --python flickervoid_round_trip.py -- <Name.glb> <Name.blend>
#
# With no Name.blend yet, the look file (the creation's greyboxes, an object per part) is imported into an empty scene and saved as
# Name.blend. From then on every save of Name.blend exports Name.glb again, which the editor's content build bakes and the creator
# shows. Parts are recognised by the fv_part custom property of each object, then by its name, then by its parent, so objects may
# be renamed, split and added to; origins are ignored (the editor's pivots win).

import os
import sys

import bpy
from bpy.app.handlers import persistent


def read_arguments():
    if "--" not in sys.argv:
        return None, None
    rest = sys.argv[sys.argv.index("--") + 1:]
    if len(rest) < 2:
        return None, None
    return os.path.abspath(rest[0]), os.path.abspath(rest[1])


LOOK, BLEND = read_arguments()


def same_file(a, b):
    return os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(b))


@persistent
def export_look(*_):
    if not bpy.data.filepath or not same_file(bpy.data.filepath, BLEND):
        return
    bpy.ops.export_scene.gltf(
        filepath=LOOK,
        export_format="GLB",
        export_extras=True,
        export_apply=True,
        use_selection=False,
    )
    print("FlickerVoid: exported " + LOOK)


def start_from_look():
    for item in list(bpy.data.objects):
        bpy.data.objects.remove(item, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=LOOK)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


if LOOK and BLEND:
    if not os.path.exists(BLEND) and os.path.exists(LOOK):
        start_from_look()
    if export_look not in bpy.app.handlers.save_post:
        bpy.app.handlers.save_post.append(export_look)
