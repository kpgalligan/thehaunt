"""farm_to_pit -- from the farmhouse's front yard, over the town to the drive-in, a glimpse
of the mansion, round onto the east entrance, the whole road west at head height (fast),
round behind the gas station and back east, to a look down on the pit.

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

Kevin's review of the first cut (2026-09-24, binding; it replaces the eastward canopy
run, the road's pace and the ending): "On the flyover, the camera should speed up
significantly when flying through the town from the east side. That takes up way too
much time. Also I want to change the path from the farm to the east side of town. Rather
than flying directly to the east side, fly towards the drive in, and steadily climb, then
descend, and bank a turn towards the east entry, looking somewhat down, flying over the
drive in, so we can see it better. While flying towards the east entrance, pan the camera
towards the mansion, so we can see it briefly in the distance. When settling in on the
pit, the camera should stay higher in the air, looking down on the pit. The angle it
finishes on does not really show the open slats and red light well."

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
                            # nearly stops at its apex, the road run speeds up sooner
PIN_ENDS = True             # start exactly at the framed spot, 5'10" up
MIN_SPEED_MS = 0.2          # the last float down is slow on purpose
BANK = (0.45, 4.0)          # a gentle lean into every turn ...
BANK_KEYS = ((0.0, 0.45, 4.0),
             (33.0, 0.45, 4.0), (34.2, 0.75, 12.0), (38.6, 0.75, 12.0), (39.8, 0.45, 4.0),
             (67.5, 0.45, 4.0), (69.5, 1.2, 15.0), (75.5, 1.2, 15.0), (77.5, 0.45, 4.0))
#                           ... (t, gain, cap deg): the banking turns over the drive-in and
#                           at the west entrance
HOLD_S = 4.0                # "where the camera should end and wait for a few seconds"

# (marker, start s, hero s): timeline markers; `hero` is the beat's still
BEATS = (
    ("1 The farmhouse", 0.0, 1.5),
    ("2 Rise past the barn", 3.0, 10.5),
    ("3 Down the drive", 14.0, 16.0),
    ("4 The corner", 18.0, 20.8),
    ("5 Toward the drive-in", 21.8, 28.8),
    ("6 Over the drive-in", 33.0, 34.0),
    ("7 The mansion", 38.0, 39.6),
    ("8 Turn west over the road", 41.0, 51.6),
    ("9 The road west", 52.0, 59.0),
    ("10 The west entrance", 67.0, 72.1),
    ("11 Behind the gas station", 74.5, 79.5),
    ("12 The pit", 88.0, 102.0),
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
    # 5 "Rather than flying directly to the east side, fly towards the drive in, and
    #   steadily climb, then descend": a left turn off the lane onto the line to the
    #   drive-in's south-west corner (south-east), climbing over the town to 68 m, then
    #   down (~22 m/s)
    (23.2, ("at", "FingerPost", 18.0, 50.0), 0.0, canopy(5.0)),
    (26.0, ("at", "Screen", -181.5, 111.7), 0.0, 45.0),
    (28.8, ("at", "Screen", -132.0, 77.5), 0.0, 68.0),      # the top: over the plaza
    (31.4, ("at", "Screen", -82.5, 43.2), 0.0, 58.0),
    # 6 "and bank a turn towards the east entry, looking somewhat down, flying over the
    #   drive in, so we can see it better": in past the screen's west end, a banked left
    #   turn (22 m radius, ~12 m/s) round the field's south-west corner, out north-east
    #   up the field's west half
    (34.0, ("at", "Screen", -40.6, 10.0), 0.0, 48.0),
    (35.1, ("at", "Screen", -28.0, 6.0), 0.0, 46.0),
    (36.1, ("at", "Screen", -17.0, 9.0), 0.0, 47.0),
    (37.1, ("at", "Screen", -9.0, 17.0), 0.0, 49.0),
    # 7 "While flying towards the east entrance, pan the camera towards the mansion, so
    #   we can see it briefly in the distance": climbing to ~55 m over the field's west
    #   half, where the line to its roofline runs up the chained drive's cut (~215 m off)
    (38.8, ("at", "Screen", 1.0, 34.0), 0.0, 54.0),
    (40.6, ("at", "Screen", 13.0, 51.0), 0.0, 56.0),
    # 8 then down to the east entry and the quick turn west (unchanged: "about 20 feet",
    #   "centered on the road"): across the road, then a tight right-hand loop over the
    #   road-out's mouth
    (43.0, ("at", "Salon", -61.0, -17.0), 0.0, 44.0),
    (45.0, ("at", "Salon", -26.0, -6.0), 0.0, 28.0),
    (46.8, ("at", "Salon", 8.0, 3.0), 0.0, 13.0),
    (48.6, ("out_e", 8.0), 3.0, ft(20)),                    # north of the line ...
    (49.8, ("out_e", 14.5), 0.0, ft(20)),                   # the hook: round through south
    (51.0, ("out_e", 12.0), -3.0, ft(20)),
    (52.0, ("out_e", 3.0), 0.0, ft(20)),                    # ... onto the centreline, heading west
    # 9 "Start flying west, slowly dropping down to about 10 feet in the air. Fly all the
    #   way down to the west entrance" + the review: "the camera should speed up
    #   significantly when flying through the town from the east side": 15 s, easing up
    #   to ~50 m/s by the police station and down again past the garage
    (53.3, ("road", "Salon", 16.0), 0.0, ft(20)),
    (54.5, ("road", "HardwareStore", 0.0), 0.0, ft(15)),
    (55.4, ("road", "PoliceStation", -10.0), 0.0, ft(10)),
    (56.9, ("road", "MansionChain", 0.0), 0.0, ft(10)),
    (59.1, ("road", "TownHall", 0.0), 0.0, ft(10)),       # ~50 m/s
    (61.2, ("road", "FingerPost", 0.0), 0.0, ft(10)),
    (63.0, ("road", "Pit", 0.0), 0.0, ft(10)),
    (65.0, ("road", "Garage", 0.0), 0.0, ft(10)),
    (67.0, ("road", "GuestCar3", 0.0), 0.0, ft(10)),        # slowing
    # 10 "then slow down and make a banking turn down until heading east again"
    (69.5, ("road", "MotelSign", 5.0), 0.0, ft(10)),
    (71.9, ("at", "MotelSign", -2.0, -9.0), 0.0, ft(10)),
    (74.1, ("at", "MotelSign", 0.0, -19.0), 0.0, ft(10)),
    # 11 "south of the gas station and garage" (5 m off their backs), then north round
    #   the big oak behind Billie's
    (76.3, ("at", "MotelSign", 10.0, -22.5), 0.0, ft(10)),
    (79.5, ("at", "GasStation", -5.0, -8.7), 0.0, ft(11)),
    (82.5, ("at", "Garage", 10.0, -8.7), 0.0, ft(11)),
    (84.7, ("at", "Pit", -70.0, 7.5), 0.0, ft(11)),
    (87.3, ("at", "Pit", -40.0, 7.5), 0.0, ft(11)),
    # 12 "Fly to a position slightly south of the pit, turn, pan down to the pit and slowly
    #    float down until the pit is centered" + the review: "the camera should stay higher
    #    in the air, looking down on the pit" (the slats and the red light between them):
    #    up to 11.5 m south of it, then floating down to 10.5 m, ~69 deg down
    (90.1, ("at", "Pit", -16.0, -4.0), 0.0, 6.0),
    (93.0, ("at", "Pit", -5.0, -8.0), 0.0, 10.5),
    (96.5, ("at", "Pit", 0.0, -5.0), 0.0, 11.5),
    (99.5, ("at", "Pit", 0.0, -4.0), 0.0, 10.5),
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
    # 5 the turn toward the drive-in: its field, far off, held as the camera climbs
    (23.0, ("at", "Screen", 0.0, 20.0), 0.0),
    (31.0, ("at", "Screen", 0.0, 20.0), 0.0),
    # 6 looking down onto the screen's face, then leading the banking turn across the
    #   field (the concession beyond)
    (33.8, ("at", "Screen", 0.0, 2.0), 5.0),
    (35.6, ("at", "Screen", 22.0, 18.0), 0.0),
    (37.0, ("at", "Concession", 0.0, 0.0), 2.0),
    # 7 the pan left to the mansion's roofline (north, up the chained drive), held
    #   briefly, then on toward the east entrance
    (38.6, ("mansion", 0.0, 0.0), -6.0),
    (40.4, ("mansion", 0.0, 0.0), -6.0),
    (42.4, ("out_e", 20.0), 3.0),
    # 8 the quick turn: from east, through south, to west, locked on the road's centre
    (48.0, ("out_e", 40.0), 4.0),
    (49.7, ("at", "Salon", 50.0, -60.0), 4.0),
    (51.2, ("out_w", 40.0), 2.5),
    # 9 down the centreline (the target beyond the west entrance: the road stays centred)
    (65.5, ("out_w", 40.0), 2.5),
    # 10 leading the banking turn: south, then east behind the forecourts
    (67.7, ("at", "MotelSign", -8.0, -14.0), 2.0),
    (71.5, ("at", "MotelSign", 4.0, -32.0), 2.0),
    (74.5, ("at", "GasStation", 0.0, -9.0), 2.5),
    # 11 east along the backs, round the oak toward the pit
    (79.5, ("at", "Garage", 25.0, -9.0), 2.5),
    (83.0, ("at", "Pit", -50.0, 7.5), 2.5),
    (86.5, ("at", "Pit", -10.0, -3.0), 1.5),
    # 12 turn to the pit (north), tilt down onto it, the pit centred at the end
    (90.5, ("at", "Pit", 0.0, 6.0), 0.5),
    (94.5, ("at", "Pit", 0.0, 1.0), 0.0),
    (99.5, ("at", "Pit", 0.0, 0.0), 0.0),
)

# (t, flyover_dusk): held at the canonical 18:00 (1.0: the lights on, the pit's red glow)
DUSK_KEYS = ((0.0, 1.0),)
