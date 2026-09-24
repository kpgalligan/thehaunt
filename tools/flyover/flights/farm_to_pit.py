"""farm_to_pit -- from the farmhouse's front yard, over the woods to the east entrance,
the whole road west at head height, round behind the gas station and back east to the pit.

Kevin (2026-09-24, binding intent): "The camera should start at about 5'10" off the
ground, to the south of the main player's house, looking directly at the house, which
should take up about 2/3rds of the view. Wait 3 seconds, then slowly rise, and pan to
the right past the barn, and start to fly down the driveway/road. Turn the corner, start
rising, and as the camera is facing south and the roadblock is in the distance, the
camera should make a sharp turn east, going slightly south, right above the tree line.
It should speed up considerably and fly towards the east road entrance. As it clears the
treeline heading in that direction, slow down, and make a quick turn facing west,
centered on the road. It should be about 20 feet in the air. Start flying west, slowly
dropping down to about 10 feet in the air. Fly all the way down to the west entrance,
then slow down and make a banking turn down until heading east again, south of the gas
station and garage. Fly to a position slightly south of the pit, turn, pan down to the
pit and slowly float down until the pit is centered, where the camera should end and
wait for a few seconds."

HOW TO TUNE: edit a number below, then in Blender press "Rebake this flight" (sidebar N
> Flyover) or run flight.rebake("farm_to_pit"); the flown table (time, height, speed per
key) prints to the text `flyover_rebake_report`. Times are seconds from the start; a
camera key's time and the next key's time set the speed between them (the table shows
it); heights are metres above the ground (ft() for feet; canopy(m) = m over the crowns).
Anchors name places in the town (see flight.py), never coordinates.
"""

from flights import canopy, ft

NAME = "farm_to_pit"
SCENE = "farm_to_pit"
CAMERA = "Cam_Flight_farm_to_pit"

LENS_MM = 35.0              # one lens throughout (a 54 deg wide view), like the film
EYE = ft(5, 10)             # 1.78 m: "about 5'10" off the ground"
HOUSE_WIDTH = 2.0 / 3.0     # "the house ... should take up about 2/3rds of the view"

# Close to the ground and under the street lights' arms: a lower ground margin, and the
# solids tested by their real surfaces (the arms reach over the road at ~8.6 m).
CLEAR_M = {"tree": 3.0, "solid": 1.0, "ground": 1.0}
EXACT_SOLIDS = True
PATH_SMOOTH_M = 2.5         # the path's corner rounding: tight enough for the hook and the bank
MAX_TURN_DEG_S = 90.0       # the "sharp" / "quick" turns: quick, bounded (checked)
AIM_SMOOTH_S = 0.5          # the view's turns ease over less than the film's 0.8 s: sharper
SPEED_SMOOTH_S = 0.9        # speed changes ease over less than the film's 1.2 s: the hook
                            # nearly stops at its apex, the east run speeds up sooner
PIN_ENDS = True             # start exactly at the framed spot, 5'10" up
MIN_SPEED_MS = 0.2          # the last float down is slow on purpose
BANK = (0.45, 4.0)          # a gentle lean into every turn ...
BANK_KEYS = ((0.0, 0.45, 4.0), (73.3, 0.45, 4.0), (75.3, 1.2, 15.0), (81.3, 1.2, 15.0), (83.3, 0.45, 4.0))
#                           ... (t, gain, cap deg): the west entrance's banking turn
HOLD_S = 4.0                # "where the camera should end and wait for a few seconds"

# (marker, start s, hero s): timeline markers; `hero` is the beat's still
BEATS = (
    ("1 The farmhouse", 0.0, 1.5),
    ("2 Rise past the barn", 3.0, 10.5),
    ("3 Down the drive", 14.0, 16.0),
    ("4 The corner", 18.0, 20.8),
    ("5 East over the trees", 21.8, 26.0),
    ("6 Turn west over the road", 33.0, 38.2),
    ("7 The road west", 40.0, 54.0),
    ("8 The west entrance", 72.8, 77.9),
    ("9 Behind the gas station", 80.3, 85.3),
    ("10 The pit", 93.8, 107.3),
)

# (t, anchor, left m, height): the first key's time is when the camera sets off.
# Speeds: the table `flyover_rebake_report` prints the flown speed at every key.
CAM_KEYS = (
    # 1 "start at about 5'10" off the ground, to the south of the main player's house,
    #   looking directly at the house, which should take up about 2/3rds of the view.
    #   Wait 3 seconds"
    (3.0, ("framed", "Farmhouse", "S", HOUSE_WIDTH), 0.0, EYE),
    # 2 "then slowly rise, and pan to the right past the barn": a crane up in place (the
    #   yard's two field trees stand across the way east), then over them, the barn left
    (6.5, ("at", "Farmhouse", 1.0, -25.5), 0.0, 10.0),
    (9.0, ("at", "Farmhouse", 2.5, -24.5), 0.0, 19.0),
    (11.0, ("at", "Farmhouse", 12.0, -20.0), 0.0, 21.0),
    (13.0, ("at", "Farmhouse", 31.0, -17.0), 0.0, 20.0),
    # 3 "and start to fly down the driveway/road" (its north edge: the field trees'
    #   crowns overhang the south one)
    (15.0, ("track", "FarmRoad", 94.0), 4.5, 10.0),
    (17.0, ("track", "FarmRoad", 84.0), 4.0, 9.0),
    # 4 "Turn the corner, start rising, and as the camera is facing south and the
    #   roadblock is in the distance" (the storm slide, down the lane)
    (18.8, ("track", "FarmRoad", 76.0), 1.0, 17.0),
    (20.3, ("track", "FarmRoad", 66.0), 0.0, 18.0),
    (21.5, ("track", "FarmRoad", 59.0), 0.0, canopy(4.0, 20.0)),
    # 5 "make a sharp turn east, going slightly south, right above the tree line. It
    #   should speed up considerably and fly towards the east road entrance."
    (22.8, ("at", "FingerPost", 14.0, 55.0), 0.0, canopy(5.0)),
    (24.5, ("at", "FingerPost", 34.0, 52.0), 0.0, canopy(4.5)),
    (27.2, ("at", "TownHall", 0.0, 32.0), 0.0, canopy(3.5)),     # ~30 m/s
    (30.2, ("at", "MansionChain", 0.0, 9.0), 0.0, canopy(3.5)),  # ~36 m/s
    (32.5, ("at", "PoliceStation", -12.0, 11.0), 0.0, canopy(3.5)),
    # 6 "As it clears the treeline heading in that direction, slow down, and make a
    #   quick turn facing west, centered on the road. It should be about 20 feet"
    (34.5, ("at", "HardwareStore", 25.0, 8.0), 0.0, 12.0),
    (36.4, ("out_e", 8.0), 3.0, ft(20)),
    (37.6, ("out_e", 14.5), 0.0, ft(20)),                   # the hook: a tight right-hand loop
    (38.8, ("out_e", 12.0), -3.0, ft(20)),                  # south of the line ...
    (39.8, ("out_e", 3.0), 0.0, ft(20)),                    # ... onto the centreline, heading west
    # 7 "Start flying west, slowly dropping down to about 10 feet in the air. Fly all the
    #   way down to the west entrance"
    (42.0, ("road", "Salon", 16.0), 0.0, ft(20)),
    (45.0, ("road", "HardwareStore", 0.0), 0.0, ft(15)),
    (47.5, ("road", "PoliceStation", -10.0), 0.0, ft(10)),
    (51.0, ("road", "MansionChain", 0.0), 0.0, ft(10)),     # ~21 m/s
    (56.2, ("road", "TownHall", 0.0), 0.0, ft(10)),
    (61.2, ("road", "FingerPost", 0.0), 0.0, ft(10)),
    (65.3, ("road", "Pit", 0.0), 0.0, ft(10)),
    (70.1, ("road", "Garage", 0.0), 0.0, ft(10)),
    (72.8, ("road", "GuestCar3", 0.0), 0.0, ft(10)),        # slowing
    # 8 "then slow down and make a banking turn down until heading east again"
    (75.3, ("road", "MotelSign", 5.0), 0.0, ft(10)),
    (77.7, ("at", "MotelSign", -2.0, -9.0), 0.0, ft(10)),
    (79.9, ("at", "MotelSign", 0.0, -19.0), 0.0, ft(10)),
    # 9 "south of the gas station and garage" (5 m off their backs), then north round
    #   the big oak behind Billie's
    (82.1, ("at", "MotelSign", 10.0, -22.5), 0.0, ft(10)),
    (85.3, ("at", "GasStation", -5.0, -8.7), 0.0, ft(11)),
    (88.3, ("at", "Garage", 10.0, -8.7), 0.0, ft(11)),
    (90.5, ("at", "Pit", -70.0, 7.5), 0.0, ft(11)),
    (93.1, ("at", "Pit", -40.0, 7.5), 0.0, ft(11)),
    # 10 "Fly to a position slightly south of the pit, turn, pan down to the pit and slowly
    #    float down until the pit is centered, where the camera should end"
    (95.9, ("at", "Pit", -14.0, -6.0), 0.0, 4.0),
    (98.8, ("at", "Pit", -3.0, -11.5), 0.0, 4.5),
    (103.3, ("at", "Pit", 0.0, -10.5), 0.0, 2.4),           # ~0.6 m/s down
)

# (t, anchor, height m): what the lens looks at; the view turns between them by angle
AIM_KEYS = (
    # 1 straight at the house (its body: the aim's height sets the tilt)
    (0.0, ("at", "Farmhouse", 0.0, 0.0), 4.0),
    (3.0, ("at", "Farmhouse", 0.0, 0.0), 4.0),
    # 2 rising on the house (the yard's trees stand between it and the barn), then over
    #   them panning right onto the barn, held as the camera passes it, then down the drive
    (7.0, ("at", "Farmhouse", 0.0, 0.0), 4.0),
    (10.5, ("at", "Barn", 0.0, 0.0), 5.0),
    (12.3, ("at", "Barn", 0.0, -3.0), 4.0),
    (14.5, ("track", "FarmRoad", 70.0), 3.0),
    # 4 round the corner: south down the lane to the storm slide
    (17.8, ("track", "FarmRoad", 44.0), 1.0),
    (21.3, ("track", "FarmRoad", 44.0), 1.0),
    # 5 the sharp turn east: toward the east entrance, low over the trees
    (22.3, ("out_e", 0.0), 8.0),
    (31.0, ("out_e", 0.0), 5.0),
    # 6 the quick turn: from east to west, locked on the road's centre far down it
    (35.2, ("out_e", 40.0), 4.0),
    (37.8, ("out_w", 40.0), 2.5),
    # 7 down the centreline (the target beyond the west entrance: the road stays centred)
    (71.3, ("out_w", 40.0), 2.5),
    # 8 leading the banking turn: south, then east behind the forecourts
    (73.5, ("at", "MotelSign", -8.0, -14.0), 2.0),
    (77.3, ("at", "MotelSign", 4.0, -32.0), 2.0),
    (80.3, ("at", "GasStation", 0.0, -9.0), 2.5),
    # 9 east along the backs, round the oak toward the pit
    (85.3, ("at", "Garage", 25.0, -9.0), 2.5),
    (88.8, ("at", "Pit", -50.0, 7.5), 2.5),
    (92.3, ("at", "Pit", -10.0, -3.0), 1.5),
    # 10 turn to the pit (north), pan down, the pit centred at the end
    (96.8, ("at", "Pit", 0.0, 7.0), 1.0),
    (103.3, ("at", "Pit", 0.0, 0.0), 0.0),
)

# (t, flyover_dusk): held at the canonical 18:00 (1.0: the lights on, the pit's red glow)
DUSK_KEYS = ((0.0, 1.0),)
