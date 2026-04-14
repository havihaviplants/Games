# Isometric Character Sheet Prompts

## Shared spec

- Purpose: presentation concept art plus future game sprite reference
- Style: 2.5D isometric, semi-SD proportions, wuxia fantasy, polished stylized render
- View: consistent isometric 3/4 top-down angle
- Lighting: soft studio lighting, clean shadows, readable silhouette
- Output: transparent background, separate character sheet per character
- Composition: one character repeated in multiple combat poses on small isometric floor tiles
- Quality target: game-ready concept sheet, crisp edges, no watermark, no text, no logo

## Master negative prompt

`low detail, blurry, distorted anatomy, broken hands, extra fingers, extra limbs, duplicate body parts, inconsistent camera angle, inconsistent costume, messy silhouette, photorealistic skin pores, background scene, heavy motion blur, cropped feet, cropped weapon, watermark, logo, text, UI, frame, collage chaos`

## Hero sheet

### Prompt

`2.5D isometric character sheet, semi-SD wuxia fantasy heroine, young female swordswoman, agile fighter, blue outfit with layered tunic, short cape, light armor accents, dark hair tied back, elegant but combat-ready, stylized polished render, transparent background, repeated full-body character on small isometric floor tiles, six combat poses: idle stance, forward slash attack, guarded ready stance, quick dodge, counterattack pose, hit reaction, clean silhouette, readable weapon shape, soft studio lighting, subtle painted shading, game concept art, consistent 3/4 isometric top-down angle, no text, no watermark`

### Short version

`semi-SD 2.5D isometric female swordswoman sheet, blue wuxia outfit, short cape, agile type, six poses, transparent background, polished stylized game render`

## Enemy 1 sheet

### Prompt

`2.5D isometric character sheet, semi-SD wuxia fantasy spear soldier, defensive archetype, red armor with layered shoulder guards, cloth underlayer, spear weapon, disciplined heavy stance, stylized polished render, transparent background, repeated full-body character on small isometric floor tiles, six combat poses: idle guard, spear thrust, hard block, lateral dodge, counter-thrust, hit reaction, clean silhouette, sturdy posture, readable spear tip, soft studio lighting, subtle painted shading, game concept art, consistent 3/4 isometric top-down angle, no text, no watermark`

### Short version

`semi-SD 2.5D isometric defensive spear soldier sheet, red armor, six poses, transparent background, polished stylized game render`

## Enemy 2 sheet

### Prompt

`2.5D isometric character sheet, semi-SD wuxia fantasy spear soldier, flanking mobile archetype, gold and ochre armor, lighter silhouette, fast footwork, spear weapon, stylized polished render, transparent background, repeated full-body character on small isometric floor tiles, six combat poses: idle ready, lunging attack, angled guard, sidestep dodge, flanking counter pose, hit reaction, clean silhouette, quick aggressive posture, readable spear line, soft studio lighting, subtle painted shading, game concept art, consistent 3/4 isometric top-down angle, no text, no watermark`

### Short version

`semi-SD 2.5D isometric mobile spear soldier sheet, gold armor, six poses, transparent background, polished stylized game render`

## Combined trio concept prompt

`beautiful 2.5D isometric wuxia fantasy battle concept, semi-SD heroine in blue facing two spear soldiers, one in red armor defensive type and one in gold armor flanking type, polished stylized render, fantasy village arena, card-battle mood, clean composition for presentation, soft warm lighting, crisp silhouettes, readable combat stances, game key visual, no watermark, no text`

## Recommended generation workflow

1. Generate the hero sheet first and lock the camera angle and rendering style.
2. Reuse that visual style for the two spear soldier sheets.
3. Generate the trio concept image last using the approved character look.
4. If the angle drifts, add: `strictly consistent isometric 3/4 top-down camera`.
5. If anatomy breaks, reduce pose complexity and regenerate one pose family at a time.

## Pose labels for production tracking

- idle
- attack
- guard
- dodge
- counter
- hit
