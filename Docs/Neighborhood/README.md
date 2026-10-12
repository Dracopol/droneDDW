# Neighborhood: three versions, three people

A small Dutch neighbourhood square for the seated VR experience, in three versions: Nature, Balanced and
Efficiency. The participant sits in the middle, looks around 360 degrees and picks the version with a
three-notch slider on the table in front of them. The street changes in a wave from the seat outwards.
Grandma, the worker and the student stay at their spots in every version, change pose and face with it, and
speak the recorded lines from "The Park Dial: Voice Script". It is dusk: lit windows, street lamps, glowing tech.

Imported on 2026-10-09 as `Assets/Street` and integrated as **Neighborhood** (it replaces the park scene as the
direction; the park is left untouched). The asset files keep their `Street_*` names: the importer, the layout and
the FBX material names depend on them.

| Version | Changing objects | Street triangles | Moving things | Grandma | Worker | Student |
|---------|-----------------:|-----------------:|---------------|---------|--------|---------|
| Nature | 259 | 38,192 | 6 ducks, the robin | feeds the ducks, happy | hands on hips at his dark lamp, angry | dog on the leash, natural |
| Balanced | 223 | 29,498 | 2 ducks, 2 drones | feeds the ducks, natural | toolbox in hand, ladder at the lamp, natural | dog and a parcel, natural |
| Efficiency | 312 | 29,846 | 4 corridor drones, 3 lane robots, 6 other drones | modern bench, no pond, angry | tablet, a robot fits the LED lamp, happy | takes a parcel from a drone, dog scared, happy |

Street triangles include the 8,952 shared ones. The three people add 31,403 triangles (grandma 10,027,
worker 12,488, student 8,888) and the miniature on the table 9,012 to 12,178.

Frame: 1 unit = 1 m, Y up, north = +Z, the seat at the origin (top at y = 0.12). The table stands 0.75 m north
of it. Eye height 1.32 m.

## Building the scene

1. Run **DroneDDW > Neighborhood > Build Neighborhood Scene** (batch mode:
   `Unity -batchmode -quit -projectPath <project> -executeMethod DroneDDW.Neighborhood.Editor.Building.NeighborhoodBuildCommandLine.Build`).
   It makes the materials, reimports the models, fills the voice library, links every WAV it finds and builds
   `Assets/Scenes/Neighborhood/Neighborhood.unity`. The scene is rebuilt from scratch every run, so anything that
   must survive goes in the builder, not in the scene.
2. The builder copies the project's XR rig from `Assets/Scenes/Questionnaire.unity` to `SeatAnchor`
   (0, 0.12, 0), facing +Z towards the table, with the camera clearing to the sky, the Interaction Tests hand
   visuals and the finger grab (shared Finger Grab Settings asset). Rebuild after changing the rig there. Without
   that rig it falls back to a desktop camera.
3. Without a headset: 1, 2, 3 pick a version, the arrow keys step, I plays the introductions, F switches between
   the "now" and "five years on" lines, R replays the reactions, S stops the voices. The controller and the voice
   director have the same buttons in their inspectors.

The scene starts at Balanced (Start Version on `NeighborhoodVersionController`, from the layout).

## Where the people stand (staging)

The export put the three people all around the seat (Grandma 48° left behind the pond at 16 m, the worker 126°
behind-left at 18 m, the student 84° right). The builder moves each of them, with the props that belong to them, to
the front half of the square, so a seated participant sees all three while using the slider:

| Person | Spot | From the seat | Moves with them |
|--------|------|---------------|-----------------|
| Grandma | east shore of the pond, turned 57° from the seat toward the ducks | 22° left, 8.0 m | her bench (all versions) |
| Worker | beside the north path, facing the seat | 11° right, 10.4 m | his lamp, ladder, toolbox, LED lamp, robot crane, robots, hovering drone |
| Student | in front of the vegetable garden, facing the seat | 25° right, 8.4 m | her dog (the leash, parcel and delivery drone hang on her) |

The spots live in `Content/Neighborhood/Data/Neighborhood Staging.asset` (made once with these defaults, never
overwritten): per person a position, a turn from facing the seat, the props that move and turn with them, and street
objects to remove where they would be in the way. Switch it off to build the street as exported. After moving them,
the builder checks every version for what a person or prop now overlaps (street objects at people's height, the pond,
the houses and the seat, the other people) and logs it; the defaults overlap nothing. The table miniature still shows
the bench and the worker's lamp at their old spots (about 3 and 9 cm on the model).

**DroneDDW > Neighborhood > Check Staging In Play Mode** checks, per version, that everyone is within 45° of straight
ahead and 5 to 12 m away, and that each person turns to the seat with their light on while speaking. Headless:
`Unity -batchmode -projectPath <project> -executeMethod DroneDDW.Neighborhood.Editor.Staging.NeighborhoodStagingCheck.Run
-stagingCaptures <folder>` (no -quit); the folder gets what the seated viewer sees per version.

## Who is talking

While a line plays, the speaker turns toward the participant (`NeighborhoodCharacterLook`: upper spine, neck and head
share the turn on top of the pose, at most 70° sideways and 20° up or down, easing in over 0.35 s and back over 0.6 s)
and a soft warm pool of light shows on the ground under them (`NeighborhoodSpeakerLight`, the lamp pools' shader, no
realtime light). In the experience they look at its head camera (`NeighborhoodExperience.AttachRig`), on their own at
the main camera. The worker's and student's pose clips hold the head 19° up, so they look slightly up when not talking.

## Voice lines

There are no WAVs yet: the 26 lines play with the mouth moving for their reading time and a warning each. Put
the WAVs anywhere under `Assets` (for example `Assets/Content/Audio/Voice/Neighborhood/`), named by their ID
(`GRA_01` to `GRA_11`, `WOR_01` to `WOR_07`, `STU_01` to `STU_08`), then run
**DroneDDW > Neighborhood > Link Voice Lines** (or rebuild).

`Content/Neighborhood/Voice/Neighborhood_VoiceLibrary.asset` holds the lines (ID, person, emotion, side, stage,
trigger, text, clip) and the reaction sets: which lines play, in order, after the street settles on a version.

| Version | Now | Five years on |
|---------|-----|---------------|
| Nature | GRA_04, WOR_02, STU_03 | GRA_05, WOR_04, STU_04 |
| Balanced | GRA_07, WOR_05, STU_05 | GRA_07, WOR_05, STU_05 |
| Efficiency | GRA_08, WOR_06, STU_06 | GRA_11, WOR_07, STU_07 |

Edit the sets in the asset (rebuilding keeps your edits). Lines not in a set (GRA_02, GRA_03, GRA_06, GRA_09,
GRA_10, WOR_03, STU_02, STU_08) are there to play by ID from experience steps. If a later layout export carries a
new script version, the next build resets the reaction sets to its defaults and links every WAV again by name.

While a line plays, the face shows its emotion and the mouth opens with the sound, measured against the line's
own recent peak. The voices are 3D sounds at each person's head, at full volume up to 10 m. The ambience dips to
45% while anyone speaks (its two loops have no clips yet).

## The slider

The table's handle carries the park's push + finger grab input, ported into this module:
`NeighborhoodSlider` (3 detents, hysteresis, soft detents while grabbed), `NeighborhoodSliderHandDriver` (any
part of a hand pushes, the finger grab takes over when the fingers close; the version commits once nothing has
touched or held the handle for 0.3 s), `NeighborhoodHandBones` (bones read from the XR hand subsystem). Push
radii, margin, push box padding and the grab box live in
`Content/Neighborhood/Data/Neighborhood Push Settings.asset` (created once, never overwritten; saved when Play
mode ends). While dragging, the miniature previews the detent's version; on commit the street changes.

## The lock-in button

Right of the slider's end sits a push button that locks the choice in (`NeighborhoodLockButton`). It works like the
Scripted Button Stepper Test in Interaction Tests, ported as `NeighborhoodPushCap`:
- Any part of a hand pushes the cap down; the cap follows the hand and springs back up once it lets go.
- Holding it pressed counts down 3, 2, 1 above the button (a tick each second, rising in pitch) and lights the 16 ring
  segments around the cap one by one. When the ring is full (3 s, `Lock Hold Seconds` in the push settings) the choice
  locks in: the Lock In sound, a flash around the ring, and the cap stays down and green until `ResetLock`.
- A press only counts while the button is armed: available, and the slider resting on a notch with no hand on it.
- The cap is grey while it can't be pressed and a steady green while it can (and once locked), with a check mark on
  it. The button and its stand turn to face the seat.

The button's collar, cap, check mark, ring segments and stand are low-poly meshes generated by
`NeighborhoodLowPolyMeshes` into `Content/Neighborhood/Models/Generated`, coloured through `Street_Palette.png`. The
parts that glow use `Street_Mat_Highlight` (the palette with its emission on but black; each renderer sets its glow).

`NeighborhoodLockButtonPlacement` picks where the table builder puts it:
- **Stand** (the default, chosen 2026-10-12): a 12 cm cap on a stand like the table, just past the table's right edge.
- **TableCorner**: between the slider's end glyph and the table's right edge, as big as fits (an 8.5 cm collar).

Both are in **DroneDDW > Experience > Build Lock Button Test Scenes** (one scene each, in passthrough), checked by
**Check Lock Button Test Scenes**.

## The slider's look

`NeighborhoodSliderLook`: when the slider's hand input switches on and nobody has touched it yet, the handle breathes a
soft glow; a hand coming close brightens it, pushing or grabbing it more. The notch markers glow, the current one
brightest, and moving the handle past a notch ticks (`Slider Notch.wav`). On the practice table only,
`NeighborhoodSliderHint` shows two bobbing orange arrows beside the handle until the slider has been moved once.

## How the change works

- One scene holds all three versions. Every object that changes has a `NeighborhoodVersionElement` with its
  versions. Ground, houses and the seat are shared and static.
- A wave starts at the seat and travels outwards at 20 m/s. When it reaches an object, the object leaves; new
  objects arrive 0.35 s later. Nothing waits longer than 2.4 s. Within 6 m of the seat everything moves 1.8 times
  slower.
- Plants, props, drones and robots grow from their base or shrink into it. Paving, the pond, buildings and the
  skyline rise out of the ground or sink into it (the towers take 2.5 times longer).
- Lamps carry their own point light and light pool, which fade with them.
- The people change pose (a 0.6 s blend) and face when the wave reaches them.
- A new choice during a change takes over from wherever each object is. Nothing snaps.

## Scripts

Runtime in `Assets/Scripts/Neighborhood` (assembly `DroneDDW.Neighborhood`), editor in
`Assets/Editor/Neighborhood` (`DroneDDW.Neighborhood.Editor`), wired together by the builder:

| Component | On | Does |
|-----------|----|------|
| `NeighborhoodVersionController` | Neighborhood_Experience | Runs the wave. |
| `NeighborhoodVersionElement` | every changing object | Its versions (mask), style (Scale or Lift), lift depth, duration scale and lights. |
| `NeighborhoodCharacter` / `NeighborhoodFace` | each person | Pose and resting face per version, speaking with the mouth moving. |
| `NeighborhoodLeash` | the leash under each dog | Keeps the line between the student's hand and the collar. |
| `NeighborhoodCharacterLook`, `NeighborhoodSpeakerLight` | each person | Turn to the participant and light under them while they speak. |
| `NeighborhoodVoiceDirector` | Neighborhood_Experience/Voice | Plays lines one at a time: reactions after each change, introductions, lines by ID. |
| `NeighborhoodMover` | drones, robots, ducks, the robin | Path, orbit, hover or hop, deterministic from Time.time. |
| `NeighborhoodExperience` | Neighborhood_Experience | Connects slider, controller, miniature and ambience through their events. |
| `NeighborhoodDebugKeys` | Neighborhood_Experience | The keys above. |
| `NeighborhoodAmbience` | Ambience | Equal-power crossfade between a nature loop and an efficiency loop; dips while someone speaks. |
| `NeighborhoodSlider`, `NeighborhoodHandBones`, `NeighborhoodSliderHandDriver` | Neighborhood_Table/Rig_Slider | The slider and its hand input (above). |
| `NeighborhoodModelPreview` | Neighborhood_Table/Rig_ModelRoot | Swaps the miniatures and turns the view cone with the head. |

Hooks: `NeighborhoodVersionController.OnVersionChange`, `OnTransitionProgress`, `OnTransitionComplete`,
`RequestVersion`, `SetVersionAsync(version, token)`, `SetVersionImmediate`, `GetWaveDelay`;
`NeighborhoodVoiceDirector.OnLineStart`, `OnLineEnd`, `PlayIntroductions`, `PlayReactions(version)`,
`PlayLine(id)`, `PlaySequenceAsync(ids, token)`, `Stop`, `Stage`, `ReactToChanges`;
`NeighborhoodCharacter.SpeakAsync`, `ShowVersion`; `NeighborhoodSlider.OnDetentChange`, `OnVersionCommit`.

Async code uses UniTask with cancellation tokens (no coroutines, no Unity Events); references are serialized by
the builder.

## Folder contents

| Path | What it is |
|------|------------|
| `Content/Neighborhood/Models/Street_Shared.fbx` | Ground, far ground, all houses with their lit windows, the seat. Static |
| `Content/Neighborhood/Models/Street_Elements.fbx` | The 702 changing objects, one node each with its pivot at its base |
| `Content/Neighborhood/Models/Street_Miniatures.fbx` | `Miniature_Nature`, `_Balanced`, `_Efficiency`, in street metres |
| `Content/Neighborhood/Models/Street_Table.fbx` | Table, plinth, slider track, three notches, handle, end glyphs, pin and view cone |
| `Content/Neighborhood/Models/Street_Grandma.fbx`, `_Worker`, `_Student` | Rig, body parts, face parts, three pose clips and four kit clips |
| `Content/Neighborhood/Layouts/Street_Layout.json` | Every element's versions and transition, lights, movers, the people, the voice script, dusk settings |
| `Content/Neighborhood/Textures/` | Palette, character texture, light pool falloff, dusk sky |
| `Content/Shaders/Neighborhood/NeighborhoodLightPool.shader` | The light pool (`DroneDDW/Neighborhood Light Pool`), URP and built-in, single pass instanced |

Models, Layouts and Textures must stay siblings: the builder finds the content root through
`Textures/Street_Palette.png`. The builder creates `Materials/`, `Animation/`, `Voice/` and `Data/` beside them
and keeps their assets (and GUIDs) on rebuilds. Glow strengths are set for the project's colour space, so build
again after switching it. Element names: `Group_Versions_NN`, e.g. `Trees_N_03` (nature only), `Paving_NB_12`
(nature and balanced), `Drones_E_04` (efficiency only).

## Lighting and Quest

Dusk: the panoramic sky, a gradient ambient read from it, linear fog, and a low warm sun from the north-west with
soft shadows. Windows, lamp heads, lit paving lines and the hub glow through emission (no HDR or bloom). Each
street lamp has a realtime point light (no shadows) and a light pool. For Quest: keep the SRP Batcher on, set the
URP asset's additional lights to Per Vertex (or Per Pixel with a per-object limit of 4), shadow distance about
40 m, HDR and post-processing off. `Street_Shared` is static with lightmap UVs if you want to bake.

## Import settings

`NeighborhoodModelPostprocessor` handles files starting with `Street_`: scale 1, hierarchy kept, no cameras or
lights. `Street_Shared` is static with lightmap UVs; the people get a Generic rig with their bones kept as objects
and looping clips. The palette is point filtered, the light pool falloff is a linear mask, the sky has no mips and
wraps horizontally.

## Fixes to Street_Grandma.fbx (Blender scripts)

Two scripts in `Docs/Neighborhood/Blender` change the exported Grandma, both applied on 2026-10-12. Each imports the FBX,
makes its change, exports with the original's settings and replaces the file only when the new one matches the
original in everything else (`street_fbx.py`: global settings, nodes, meshes, bind matrices, clips and their keys, and
every other bone within 0.5 mm in every clip). Run them again, in this order, after a fresh export; each leaves a file
it has already fixed alone. Unity may stay open: it reimports the FBX when it gets focus, then rebuild the scene.

- `fix_grandma_trousers.py`: the trousers (`Grandma_Shorts`) were stretched shorts, with no edge loop at the knee and no
  shin weights, so in her sitting poses her legs came through them by up to 16 cm. It splits each trouser leg into 8
  segments and weights it from the body's hip and leg bones; the body now stays within 6 mm of the trousers in every
  clip. `-- --check` only measures.
- `rest_grandma_hands.py`: in her sitting poses her arms hovered in the air (one hand reaching for the ducks). It
  re-poses only her arms, frame by frame and keeping her breathing: wrists on top of her thighs halfway to the knee,
  elbows out and back, palms down with the fingers toward the knees.

`"C:\Blender\blender.exe" -b --factory-startup --python Docs/Neighborhood/Blender/<script>.py` (add `-- --renders <folder>`
for before and after pictures).

## Regenerating

The Blender sources (`BlenderSource/`), previews and tests from the original package were left out. A fresh
export writes `Models`, `Layouts` and `Textures` folders: copy them over the ones in `Assets/Content/Neighborhood`
and run the builder again.
