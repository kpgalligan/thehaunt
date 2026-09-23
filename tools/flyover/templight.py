"""TEMPORARY readability lighting for the Phase 3 checks: one low autumn sun from the
south-west and a flat sky-day world. Phase 8 (look-dev) replaces this whole module
and the Flyover_TempLight collection; nothing else depends on it."""

import math

import bpy

import config
import scene

SUN_NAME = "TempSun"
WORLD_NAME = "Flyover_TempSky"


def build(col):
    sun = scene.tag(bpy.data.lights.new(SUN_NAME, "SUN"))
    sun.energy = 3.2
    sun.angle = math.radians(2.0)
    sun.color = (1.0, 0.95, 0.86)
    ob = scene.new_object(SUN_NAME, sun, col)
    ob.rotation_euler = (math.radians(52), 0.0, math.radians(-40))   # from the south-west
    w = scene.tag(bpy.data.worlds.new(WORLD_NAME))
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = config.hex_rgba(config.PALETTE["sky-day"])
    bg.inputs["Strength"].default_value = 0.9
    bpy.context.scene.world = w
    return ob
