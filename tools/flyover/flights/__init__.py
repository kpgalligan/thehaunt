"""The flight registry: every named camera sequence of the flyover, one data module each.

A sequence module is a STORYBOARD only (flight.py is the machinery: anchors, the path,
timing, the clearance check, the bake). Each lives in its own Blender scene sharing the
one Flyover world: its own camera, frame range 1..n, beat markers and `flyover_dusk` keys.

    NAME, CAMERA            the sequence id and its camera object (Cam_Flight_<name>)
    SCENE                   its scene's name; None = the host scene (PRIMARY only)
    BEATS                   ((marker, start s, hero s), ...): timeline markers + stills
    CAM_KEYS                ((t s, anchor, left m, height), ...): where the lens is
    AIM_KEYS                ((t s, anchor, height), ...): what it looks at
    DUSK_KEYS               ((t s, flyover_dusk), ...): the time of the light (1 = 18:00)
    HOLD_S                  the still hold after the camera settles at the last key

Optional (default config.FLIGHT_*): LENS_MM, CLEAR_M {tree, solid, ground} (m past the
0.5 m lens radius), PATH_SMOOTH_M, SPEED_SMOOTH_S, AIM_SMOOTH_S, FLOOR_SMOOTH_S, BANK
(gain, cap deg), BANK_KEYS ((t, gain, cap deg), ...: a deliberate banking turn), MAX_TURN_DEG_S, MIN_SPEED_MS,
START_EASE_S, PIN_ENDS (the path starts / ends exactly on its first / last key: the
corner blur otherwise pulls them in), EXACT_SOLIDS (test solids by their real surfaces, so the lens may pass
UNDER a street light's arm; default False: every solid is a block from the ground up).

The first camera key's time is when the camera sets off: before it the camera and the
view hold still (a sequence can open on a held frame).

Heights (a key's height): a number = metres above the ground (Terrain.z_at);
`ft(feet, inches)` converts; `canopy(m, r)` = m above the highest tree crown within r m
of the key (the forest's real kit heights), or above the ground where no tree stands.
"""

import importlib

NAMES = ("town_pass", "farm_to_pit")
PRIMARY = "town_pass"       # the host scene's sequence: the film


def load(name):
    """The sequence module, reloaded (so an edit applies without restarting Blender)."""
    if name not in NAMES:
        raise KeyError(f"unknown flight '{name}': {NAMES}")
    return importlib.reload(importlib.import_module(f"flights.{name}"))


def ft(feet, inches=0.0):
    """Feet (+ inches) in metres."""
    return (feet + inches / 12.0) * 0.3048


def canopy(m, r=25.0):
    """A height: m metres over the highest crown within r metres (see the module doc)."""
    return ("canopy", float(m), float(r))
