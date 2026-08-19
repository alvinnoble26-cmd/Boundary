## Project goal

Create a polished two-player multiplayer arena game with satisfying movement,
distinct abilities, readable combat feedback, memorable maps, and a complete
mobile/App Store release.

## Status legend

- `[ ]` Not started
- `[-]` In progress
- `[x]` Complete
- `[?]` Needs clarification
- `[!]` Blocked

## Priority legend

- P0 — Required for the game to function or ship
- P1 — Important for the next playable milestone
- P2 — Valuable polish or content
- P3 — Experimental or future idea

---

# Milestone 1: Playable core

## 1.1 Movement

- [ ] 1.1.01.00 Crouching — P1
- [ ] 1.1.02.00 Sprinting — P1
- [ ] 1.1.03.00 Sliding — P1
- [ ] 1.1.04.00 Slide-then-jump transition — P1; depends on Sliding
- [ ] 1.1.05.00 Double jump — P1
- [ ] 1.1.06.00 Wall movement or wall jump — P1
- [ ] 1.1.07.00 Grappling gun — P2
- [ ] 1.1.08.00 Gravity flip — P2
- [ ] 1.1.09.00 Time slow — P2
- [ ] 1.1.10.00 Feather falling — P2
- [ ] 1.1.11.00 Rewind — P3
- [ ] 1.1.12.00 Charge jump — P2
- [ ] 1.1.13.00 Intangibility — P2

## 1.2 Core combat abilities

- [ ] 1.2.01.00 Teleport ability — P1
  - Done when: wind-up, execution, cooldown, and multiplayer synchronization work.
- [ ] 1.2.02.00 Slow down teleport animation — P2; depends on Teleport ability
- [ ] 1.2.03.00 Object throw — P1
- [ ] 1.2.04.00 Block — P2
- [ ] 1.2.05.00 Improve black hole visual effect — P2
- [ ] 1.2.06.00 Add stronger impact to ability button taps — P2
- [ ] 1.2.07.00 Make skin arms show in the abilities - P1

---

# Milestone 2: Ability quality and combat readability

## 2.1 Ability polish

- [ ] 2.1.01.00 Add visual effects to abilities
- [ ] 2.1.02.00 Add sound effects to abilities
- [ ] 2.1.03.00 Add clearer ability cooldown feedback
- [ ] 2.1.04.00 Add hit confirmation
- [ ] 2.1.05.00 Add damage feedback
- [ ] 2.1.06.00 Make enemy collisions easier to understand
- [ ] 2.1.07.00 Make it easier to tell when the player is hit
- [ ] 2.1.08.00 Add near-death audio
- [ ] 2.1.09.00 Add vibration feedback
- [ ] 2.1.10.00 Add ability introduction camera effect
- [ ] 2.1.11.00 Add camera-shutter-style ability introduction
- [ ] 2.1.12.00 Add Smash Bros.-style ability introduction
- [ ] 2.1.13.00 Add teleport wind-up effect
- [ ] 2.1.14.00 Make ability effects readable to both players
- [ ] 2.1.15.00 Make the slide ability shrink affect seen by both players (it should not just be local)
- [ ] 2.1.16.00 Upgrade the slide ability by adding a wall slides and a slide jump.

## 2.2 Combat UI

- [ ] 2.2.01.00 Health bar
- [ ] 2.2.02.00 Death screen
- [ ] 2.2.03.00 Crosshair
- [ ] 2.2.04.00 Improve player damage indicators

---

# Milestone 3: Maps and arena presentation

## 3.1 Maps

- [ ] 3.1.01.00 Fix the current map
  - Notes: Record the scene, symptom, reproduction steps, and expected result.
- [ ] 3.1.02.00 Add another level
- [ ] 3.1.03.00 Add moving objects to the map
- [ ] 3.1.04.00 Add barrels
- [ ] 3.1.05.00 Add floating orbs
- [ ] 3.1.06.00 Add wind
- [ ] 3.1.07.00 Add lighting pass
- [ ] 3.1.08.00 Add reflections
- [ ] 3.1.09.00 Improve map readability
- [ ] 3.1.10.00 Add missing map sprites
- [ ] 3.1.11.00 Add map-specific hazards or events

## 3.2 Technical art and audio

- [ ] 3.2.01.00 Organize prefabs
- [ ] 3.2.02.00 Organize shaders
- [ ] 3.2.03.00 Organize textures
- [ ] 3.2.04.00 Organize materials
- [ ] 3.2.05.00 Add particle effects
- [ ] 3.2.06.00 Add map sound effects
- [ ] 3.2.07.00 Add music

---

# Milestone 4: Skins and progression

## 4.1 Skins

- [ ] 4.1.01.00 Add Ryan skin
- [ ] 4.1.02.00 Add Mahoraga skin
- [ ] 4.1.03.00 Add Sun Ducker skin
- [ ] 4.1.04.00 Make the player arm visible while throwing
- [ ] 4.1.05.00 Make the player arm visible while using abilities
- [ ] 4.1.06.00 Add skin previews
- [ ] 4.1.07.00 Fix buying skins
  - Notes: Identify whether the problem is purchase verification, ownership,
    UI state, persistence, or Firebase data.
- [ ] 4.1.08.00 Make purchased skins persist correctly
- [ ] 4.1.09.00 Give skins distinct abilities or identities
- [ ] 4.1.10.00 Define whether skin abilities are cosmetic, balanced, or competitive
- [ ] 4.1.11.00 Verify skin behavior for both players in multiplayer

## 4.2 Shop and progression

- [ ] 4.2.01.00 Improve skin shop UI
- [ ] 4.2.02.00 Show ownership clearly
- [ ] 4.2.03.00 Show selected/equipped skin clearly
- [ ] 4.2.04.00 Add purchase failure feedback
- [ ] 4.2.05.00 Add restore-purchase behavior where needed

---

# Milestone 5: Menus and user experience

## 5.1 Menus and controls

- [ ] 5.1.01.00 Add a back button
- [ ] 5.1.02.00 Make different screens use consistent back behavior
- [ ] 5.1.03.00 Add button press effects
- [ ] 5.1.04.00 Add menu sound effects
- [ ] 5.1.05.00 Improve menu transitions
- [ ] 5.1.06.00 Improve side-swipe joystick controls
- [ ] 5.1.07.00 Fix wall/camera glitch
- [ ] 5.1.08.00 Add settings for sound and vibration
- [ ] 5.1.09.00 Add clearer ability onboarding
- [ ] 5.1.10.00 Add loading and connection feedback
- [ ] 5.1.11.00 Add rematch and return-to-menu feedback

---

# Milestone 6: Multiplayer and event reliability

## 6.1 Multiplayer and events

- [ ] 6.1.01.00 Fix events
  - Notes: Identify the affected event, script, reproduction steps, and expected result.
- [ ] 6.1.02.00 Verify ability replication
- [ ] 6.1.03.00 Verify player spawning
- [ ] 6.1.04.00 Verify remote-player visuals
- [ ] 6.1.05.00 Verify damage occurs only once
- [ ] 6.1.06.00 Verify match results are recorded only once
- [ ] 6.1.07.00 Verify disconnect behavior
- [ ] 6.1.08.00 Verify rematch behavior
- [ ] 6.1.09.00 Verify scene transitions
- [ ] 6.1.10.00 Verify skin synchronization
- [ ] 6.1.11.00 Verify server-authoritative ability validation

---

# Milestone 7: Marketing and release

## 7.1 Marketing

- [ ] 7.1.01.00 Create preview trailer
- [ ] 7.1.02.00 Create Sun Ducker marketing material
- [ ] 7.1.03.00 Add France
  - Notes: Clarify whether this means localization, a map, marketing, or App Store territory support.

## 7.2 App Store

- [ ] 7.2.01.00 Prepare the game for App Store submission
- [ ] 7.2.02.00 Add required App Store assets
- [ ] 7.2.03.00 Fix App Store review-related issues
- [ ] 7.2.04.00 Verify privacy and support pages
- [ ] 7.2.05.00 Verify purchase flow
- [ ] 7.2.06.00 Verify production Firebase configuration
- [ ] 7.2.07.00 Verify production Edgegap configuration
- [ ] 7.2.08.00 Create a release build
- [ ] 7.2.09.00 Complete the multiplayer release checklist in `DEPLOYMENT.md`

---

# Experimental ideas

These remain separate from required work until their purpose and smallest
prototype are defined.

## 8.1 Experimental ideas

- [ ] 8.1.01.00 Elemental system
- [ ] 8.1.02.00 Flip ability
- [ ] 8.1.03.00 Purple Hollow
- [ ] 8.1.04.00 Domain Expansion
- [ ] 8.1.05.00 New singularity concept
- [ ] 8.1.06.00 Tie mechanic
  - Notes: Define when a tie can happen and what the match result should be.
- [ ] 8.1.07.00 Additional map themes

For each experimental idea, define:

- Player-facing behavior
- Why the game needs it
- Existing systems to reuse
- Smallest prototype
- Test plan
- Whether it is competitive, cosmetic, or experimental

---

# Bugs and unclear tasks

## 9.1 Bugs and unclear tasks

- [ ] 9.1.01.00 Clarify `Ryan`
  - Notes: Determine whether this is a skin, character, marketing item, or owner.
- [ ] 9.1.02.00 Clarify `Maho`
  - Notes: Determine whether this refers to Mahoraga.
- [ ] 9.1.03.00 Clarify `NT`
- [ ] 9.1.04.00 Clarify `Mark`
- [ ] 9.1.05.00 Clarify `Add France`
- [ ] 9.1.06.00 Clarify `New singularity idea`

---

# Definition of done

## 10.1 Completion requirements

- [ ] 10.1.01.00 Requested player-facing behavior works in the relevant scene.
- [ ] 10.1.02.00 Affected UI, animation, audio, and visual feedback are integrated.
- [ ] 10.1.03.00 Multiplayer authority and remote-player behavior are correct when applicable.
- [ ] 10.1.04.00 Relevant Unity tests or validation checks were run.
- [ ] 10.1.05.00 No new Unity Console errors or warnings were introduced.
- [ ] 10.1.06.00 Existing related behavior still works.
- [ ] 10.1.07.00 Manual Unity Editor test steps were completed.
- [ ] 10.1.08.00 A completion note was added.

## Completion note template

- Completed:
- Files changed:
- Tests/checks run:
- Manual testing:
- Known limitations:
- Follow-up tasks:

---

# Completed work

Move completed tasks here periodically, or leave checked tasks in their
milestone until the milestone is finished.
''

# Specifics

1.2.07.00 
- I want the skins arm to be seen when activating the abilities. This should be affect for all of the skins(Beard, Turtle, and Sun Ducker).
- For each ability the arm animation will be slightl different. For the throw abilities, the arm will simply be across the screen. Tthe arm should not block the screen. It should be small and point to the direction the orb is going.
- The turtle arm should be green. The beard skin arm should be white. The Sun Ducker arm should be black.
- For the teleport ability the arm should swing across the screen. This should take 0.5 seconds. When this is done the player will teleport.
- For the slide and dash ability the arm should be on the side  for the duration of the slide/dash.
- the arm should be shown on the game camera so make sure it is seen.




2.1.13.00
- The wind up to the teleport ability should include the arm animation(which is already made) and the character should spin in a 360 while it is in a paused state. this spin should take 0.5 seconds. After the spin the teleport should take place.
- The other player should be able to see the spin. It should not be local. It should work with multiplayer
- The particle affect for the teleport should be active during the spin





2.1.16.00
- The slide ability should be canceled by a jump. This jump should be higher than the normal jump.
- you should be able to slide on walls.
- if the player runs out of floor/wall to slide on it should automatically jump up


3.1.06.00
- Search for a wind particle affect in the assets. See which on is the coolest. Give a wind affect to players running.
- Add extra wind to a players screen when they are goin above a certain speed. For example if they are using the slide or dash ability.


5.1.01.00
- the back button should be in the topright of the screen
- it should be an x
- after pressing it once it should say hold to exit
- after holding it should bring you back to the menu scene specifically the play panel. 


1.1.07.00
- Add a grappling ability. The ability should be chosen in the abilities panel and should follow the same flow as the rest of the abilities.
- When you press the button you arm will shoot a black string to where ever your crosshair is pointing.
- the string will then attach to the object you are looking at and it will lunge you forward.
- This should include the wind animation
- it should have a 3 second cool down
- if there is no object that you are looking at the ability wont be able to be pressed.
- to cancel mid grappling players can press the jump button. which will still jump but the rope will stop pulling you. 
- if a player grapples onto an object like a black hole or cube the cube will come to them. They can cancel this as will by pressing jump.


2.1.09.00
- Add Vibrations when a players touches a black hole and black cube.
- The vibrations should work on any iPhone






# Small Fixes
- Change repel throw
- increase TP distance
- INcrease gravity