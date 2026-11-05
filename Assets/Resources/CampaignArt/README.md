# Campaign art sources and import settings

Retrieved on 2026-11-05 from Kenney's official website. All 18 images are unmodified PNG files from the named packs. Renamed filenames are mapped to original files and SHA-256 hashes in `manifest.json`.

| Pack | Official source | License retained in this folder |
| --- | --- | --- |
| Space Shooter Extension | https://kenney.nl/assets/space-shooter-extension | `LICENSE-Extension.txt` |
| Space Shooter Remastered (the current name of Space Shooter Redux) | https://kenney.nl/assets/space-shooter-remastered | `LICENSE-Remastered.txt` |
| Particle Pack 1.1 | https://kenney.nl/assets/particle-pack | `LICENSE-Particles.txt` |

Author: Kenney Vleugels / https://kenney.nl. All three original pack licenses permit personal and commercial use under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). Credit is appreciated and optional. Original license files are copied without edits. No paid package, account login, or Unity Asset Store installation is required.

## Selected resources

Use `Resources.Load<Sprite>("CampaignArt/<name>")`, omitting `.png`.

| Name | Role | Original pack/file |
| --- | --- | --- |
| `boss-stage1` | Wide battle cruiser hull | Extension `Ships/spaceShips_001.png` |
| `boss-stage2` | Heavy armored hull | Extension `Ships/spaceShips_005.png` |
| `boss-stage3` | Four-wing final battleship hull | Extension `Ships/spaceShips_007.png` |
| `meteor-a`, `meteor-b` | Stage 2/3 obstacles | Extension `Meteors/spaceMeteors_001.png`, `004.png` |
| `boss-missile` | Homing missile | Extension `Missiles/spaceMissiles_001.png` |
| `boss-bullet` | Red barrage projectile | Remastered `Lasers/laserRed01.png` |
| `drop-blackhole`, `drop-slow`, `drop-time` | Skill charge pickups | Remastered blue/green/yellow star powerups |
| `drop-reflect`, `drop-shield` | Reflection and shield pickups | Remastered red/blue shield powerups |
| `drop-heal` | Health pickup | Remastered `Power-ups/pill_green.png` |
| `drop-invulnerable` | Invulnerability pickup | Remastered `Power-ups/powerupYellow_bolt.png` |
| `opening-ship` | Optional opening illustration | Remastered `playerShip3_blue.png` |
| `explosion` | Boss death / impact fire | Particle Pack transparent `fire_01.png` |
| `nebula-smoke` | Optional local nebula effect | Particle Pack transparent `smoke_01.png` |
| `engine-glow` | Optional opening / engine effect | Particle Pack transparent `light_01.png` |

The existing team stage backgrounds (`Assets/Jain/Art/Stage1BG.jpg`, `Stage2BG.jpg`, `Stage3BG.jpg`) and UI artwork are preserved. The additions use the existing shaded flat 2D style and do not replace player or ordinary enemy artwork.

## Unity 6 compatibility and verification

The added files contain no scripts, packages, shaders, materials, or render-pipeline dependencies. Their metadata configures single full-rectangle sprites, 100 pixels per unit, centered pivots, alpha transparency, bilinear filtering, clamp wrapping, no mipmaps, and no generated physics shapes. Bosses and obstacles use explicit runtime collision geometry rather than automatic image outlines. Projectiles point up in their source image.

PNG signatures, image dimensions, file hashes, deterministic metadata GUIDs, and resource filenames were checked without launching Unity. Actual import and visual behavior in the project's Unity 6000.5.0f1 / URP 2D configuration still require the user's editor check. Runtime loading should use SpriteRenderer with the project's existing sprite material or a URP-compatible unlit sprite material. Particle textures are ordinary sprites; this change does not import an older Particle System shader.

## Manual re-download / verification

1. Open the official source page for the required pack.
2. Select **Download**, then **Continue without donating**. This is a free download; the site currently generates a short-lived download link.
3. Extract the ZIP outside `Assets`, compare the original license text, and use the original path in `manifest.json` to locate the selected PNG.
4. Preserve this folder's `.meta` files when replacing an image so references remain stable. Do not import the whole pack or replace the team's UI/backgrounds.
5. In Unity, confirm each added image is a single sprite, then check boss hull size/orientation, projectile visibility, pickup icons, and explosion transparency during Play mode. These are manual checks, not reported as passed here.

Cached old direct ZIP URLs may redirect to HTML. Use the official source page instead of relying on a permanent archive URL.
