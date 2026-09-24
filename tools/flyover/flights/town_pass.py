"""town_pass -- the film (Phase 9): road in from the west, the motel, Billie's, a look
up the farm road, the plaza, the drive-in and the mansion glimpse, out east, and the
turn back over the valley. 71.0 s. The storyboard as Kevin signed it off for the 1080p
review cut: its content is unchanged since Phase 10 (the frames reproduce).

Keys: see flight.py (anchors, heights above the ground, left offsets). Every setting
this file leaves out is config.FLIGHT_* (lens 35 mm, clearance tree 3 / solid 1.5 /
ground 2.5 m, path blur 6 m, speed blur 1.2 s, view turn <= 40 deg/s, bank <= 4 deg).
"""

NAME = "town_pass"
SCENE = None                # None = the host scene (the build's own scene, the film)
CAMERA = "Cam_Flight_town_pass"

# (marker, start, hero): the timeline marker at `start`; `hero` is the beat's still.
BEATS = (
    ("1 Road in", 0.0, 3.0),
    ("2 West Entry", 6.3, 7.8),
    ("3 Billie's", 16.0, 18.8),
    ("4 The Fork", 22.5, 27.0),
    ("5 Town", 30.5, 34.8),
    ("6 East Fork", 38.0, 46.4),
    ("7 East Entry", 48.5, 52.0),
    ("8 The valley", 58.0, 69.0),
)
HOLD_S = 3.0                # the camera rests this long after it settles (title / fade room)

# (t, anchor, left m, height m)
CAM_KEYS = (
    # 1 low over the road deep in the forest, travelling east round the road-out's bend
    (0.0, ("out_w", 150.0), 0.0, 12.0),
    (3.0, ("out_w", 80.0), 0.0, 10.0),
    (5.6, ("out_w", 22.0), -1.0, 8.5),
    # 2 the motel's pole sign at eye level on the left, the lot, the gas station across
    (8.4, ("road", "MotelSign", 3.0), -6.0, 7.0),
    (12.2, ("road", "GuestCar3", 2.0), -3.0, 8.5),
    (15.3, ("road", "Garage", 4.5), -1.0, 9.5),
    # 3 Billie's lit windows; slowing, a dip toward the pit's cover ahead on the right
    (18.2, ("road", "Bar", 0.0), -2.0, 8.0),
    (20.8, ("road", "Pit", -6.0), -4.5, 6.5),
    # 4 the fork: rising a touch to look up the farm road through the treeline
    (24.4, ("road", "FingerPost", -35.0), 0.0, 11.0),
    (27.2, ("road", "FingerPost", -3.0), -4.0, 19.0),
    (30.0, ("road", "FingerPost", 24.0), -3.0, 18.0),
    # 5 south of the plaza: the town hall, the well and its lamps
    (33.2, ("at", "Well", -40.0, -22.0), 0.0, 17.0),
    (36.4, ("at", "Well", -10.0, -24.0), 0.0, 20.0),
    # 6 rising over the trees: the drive-in off to the right, then a left turn up the
    # chained drive: the ruin's roofline over the canopy
    (40.4, ("road", "TownHall", 55.0), -26.0, 38.0),
    (43.3, ("at", "MansionChain", -10.0, -14.0), 0.0, 38.0),
    (45.8, ("at", "MansionChain", -5.0, 20.0), 0.0, 38.0),
    (47.4, ("at", "MansionChain", 6.0, 29.0), 0.0, 40.0),
    (49.0, ("at", "MansionChain", 25.0, 20.0), 0.0, 40.0),
    # 7 turning back to the road: police, hardware, salon; out along the road-out
    (50.8, ("at", "MansionChain", 46.0, -8.0), 0.0, 33.0),
    (52.6, ("road", "PoliceStation", -6.0), -4.0, 16.0),
    (54.8, ("road", "HardwareStore", 10.0), -3.0, 13.0),
    (57.5, ("out_e", 45.0), 4.0, 40.0),
    # 8 the reveal: up over the forest north of the road-out, turning back to the valley
    (61.0, ("out_e", 105.0), 22.0, 45.0),
    (65.0, ("out_e", 150.0), 48.0, 68.0),
)

# (t, anchor, height m)
AIM_KEYS = (
    (0.0, ("out_w", 50.0), 3.0),
    (3.0, ("road", "MotelSign", -5.0), 4.0),
    (5.6, ("at", "MotelSign", 0.0, 0.0), 5.5),
    (7.6, ("at", "MotelSign", 0.0, 0.0), 5.5),
    # the car's dump rect grew north to its nose-in footprint (2026-09-24): -2.5 m holds
    # the aim the review cut was flown on (the old one-row rect's centre)
    (9.8, ("at", "GuestCar3", 0.0, -2.5), 1.0),
    (12.2, ("at", "GasStation", 0.0, 0.0), 3.0),
    (13.6, ("at", "Garage", 0.0, 0.0), 3.0),
    (15.8, ("at", "Bar", 0.0, -3.0), 3.0),
    (19.0, ("at", "Pit", 10.0, 6.0), 0.5),
    (20.0, ("at", "Pit", 10.0, 6.0), 0.5),
    (22.0, ("road", "FingerPost", 5.0), 3.0),
    (26.4, ("track", "FarmRoad", 62.0), 4.0),
    (28.8, ("at", "Barn", 0.0, 0.0), 6.0),
    (32.4, ("at", "TownHall", 0.0, 0.0), 7.0),
    (35.8, ("at", "TownHall", 0.0, 0.0), 9.0),
    (39.4, ("at", "Screen", 0.0, 0.0), 5.0),
    (40.7, ("at", "Screen", 0.0, 0.0), 5.0),
    (44.7, ("mansion", 0.0, 0.0), -3.0),
    (47.2, ("mansion", 0.0, 0.0), -3.0),
    (50.4, ("road", "HardwareStore", 30.0), 4.0),
    (52.9, ("road", "HardwareStore", 45.0), 3.0),
    (55.0, ("out_e", 60.0), 4.0),
    (57.6, ("out_e", 190.0), 20.0),
    (61.4, ("at", "MansionChain", 210.0, 300.0), 40.0),
    (65.6, ("at", "TownHall", -40.0, 0.0), 0.0),
)

# (t, flyover_dusk): 17:43 light in the forest (0.86 of 16:00 -> 18:00); the neon cuts
# on with the motel's sign centre frame (DUSK_GATE's 0.97-0.995 ramp); 18:00 on. Phase 10
# moved it from 7.4 s so the V has room to blink in full view (config.MOTEL_V_FIRST_OFF_S:
# lit ~6.7 s, V off 7.2-7.75 s, back on before the sign leaves frame at ~8.2 s).
NEON_ON_S = 6.3
DUSK_KEYS = ((0.0, 0.86), (NEON_ON_S - 0.3, 0.968), (NEON_ON_S + 0.5, 1.0))
