# The Painting Toolbar: A Guide to Every Tool

---

## Overview

The **Painting Toolbar** is the narrow vertical strip on the left side of the CAIME editor window. Think of it like the toolbox in a painting programme such as Adobe Photoshop or GIMP — it holds every instrument you use to mark up your campaign map. Instead of painting colours for artistic purposes, each tool here places *game data* onto the hex grid: terrain types, region boundaries, roads, rivers, and more.

There are eight tools in total, each designed for a different painting scenario. You can only use one tool at a time, and each has a keyboard hotkey so you can switch between them instantly without reaching for the mouse. The tool that is currently active appears highlighted (pressed-in) in the toolbar.

---

## Table of Contents

1. [How the Toolbar Works](#1-how-the-toolbar-works)
2. [The Quick-Settings Bar — Tool Controls at the Top](#2-the-quick-settings-bar--tool-controls-at-the-top)
   - [Brush Size Slider](#brush-size-slider)
   - [Image Opacity Slider](#image-opacity-slider)
   - [Clear Background Image Button](#clear-background-image-button)
   - [Flood Fill Source Dropdown](#flood-fill-source-dropdown)
3. [Tool Reference — Every Tool Explained](#3-tool-reference--every-tool-explained)
   - [Pan — Navigate the Map](#pan--navigate-the-map)
   - [Zoom — Zoom In and Out](#zoom--zoom-in-and-out)
   - [Brush — Paint Individual Hexes](#brush--paint-individual-hexes)
   - [Flood Fill — Fill an Entire Area at Once](#flood-fill--fill-an-entire-area-at-once)
   - [Eraser — Remove Paint from Hexes](#eraser--remove-paint-from-hexes)
   - [Background Image — Load a Reference Image](#background-image--load-a-reference-image)
   - [Line — Paint a Straight Line of Hexes](#line--paint-a-straight-line-of-hexes)
   - [Color Picker — Sample a Colour from the Canvas](#color-picker--sample-a-colour-from-the-canvas)
4. [Keyboard Shortcuts — All Eight Tools at a Glance](#4-keyboard-shortcuts--all-eight-tools-at-a-glance)
5. [Pro-Tips & Troubleshooting](#5-pro-tips--troubleshooting)

---

## 1. How the Toolbar Works

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  File    Edit    Process    Tools    Settings    Help                        │
├────┬────────────────────────────────────────────────────────────────────────┤
│    │  Brush Size: ───●── 3   │  Image Opacity: ────●── 100  │ [✕]  Source:│
│ ┌──┤                                                                        │
│ │M │ ← Pan tool (active — shown highlighted)                                │
│ │Z │ ← Zoom tool                                                            │
│ │B │ ← Brush tool                                                           │
│ │F │ ← Flood Fill tool                                                      │
│ │E │ ← Eraser tool                                                          │
│ │I │ ← Background Image tool                                                │
│ │L │ ← Line tool                                                            │
│ │P │ ← Color Picker tool                                                    │
│ │──│                                                                        │
│ │⚙ │ ← Properties button (reserved for future use)                          │
│ └──┤                                                                        │
│    │                      MAP CANVAS                                        │
└────┴────────────────────────────────────────────────────────────────────────┘
```

**To activate a tool:**

1. **Click** its icon in the Toolbar, **or**
2. Press its **keyboard hotkey** (shown next to each tool name in this guide).

The currently active tool's button will appear pressed-in / highlighted. Only one tool can be active at a time.

> **Important:** Before any painting tool will work, you must first:
> - Have a project open (**File → Open** or **File → Create new map**).
> - Select an active **Layer** in the **Layers** panel on the right sidebar.
> - Select an active **Swatch** in the **Swatches** panel on the right sidebar.
>
> Without a selected layer and swatch, the Brush, Flood Fill, Eraser, and Line tools have nothing to paint with.

---

## 2. The Quick-Settings Bar — Tool Controls at the Top

```
  Brush Size: ─────●──── 5   Image Opacity: ──────●──── 100   [✕]   Source: [▼ Ground Types]
```

The **Quick-Settings Bar** sits in the horizontal strip above the map canvas. It contains controls that adjust how the currently selected tool behaves. Some controls appear or disappear depending on which tool is active.

---

### Brush Size Slider

```
  Brush size: [──●──────] 2
               ↑ min=1     max=10 ↑
```

| Detail | Value |
|--------|-------|
| **Range** | 1 (single hex) to 10 (large multi-hex area) |
| **Visible when** | The **Brush** or **Eraser** tool is active |
| **Current size number** | Displayed next to the slider |

Drag the slider to the **right** to paint a wider area with each stroke. Drag to the **left** to narrow it down to a single hex for precise work.

> **Tip:** For broad strokes (filling large terrain regions), set size to 7–10. For detail work along coastlines or river edges, drop it to 1 or 2.

---

### Image Opacity Slider

```
  Image opacity: [──────────●] 100
                  ↑ 0=hidden       100=fully visible ↑
```

| Detail | Value |
|--------|-------|
| **Range** | 0 (completely hidden) to 100 (fully opaque) |
| **Visible when** | Always visible, but only enabled when a background reference image is loaded |

Controls how visible the background reference image (loaded with the **Background Image** tool) appears behind the hex grid. Setting it to 50 gives a 50% transparent blend — you can see the reference image and the painted hexes at the same time.

---

### Clear Background Image Button

```
  [✕]  ← Click to remove the background reference image
```

Clicking this button removes the reference image that was loaded with the **Background Image** tool. It only becomes clickable when a background image is currently loaded. Clearing the image does not affect any painted layer data — only the reference image behind the grid is removed.

---

### Flood Fill Source Dropdown

```
  Source: [ Ground Types   ▼ ]
```

| Detail | Value |
|--------|-------|
| **Visible when** | The **Flood Fill** tool is active |
| **What it controls** | Which layer defines the shape boundary for the flood fill |

By default, the Flood Fill tool uses the **currently active layer** as its boundary — it fills until it hits a hex that already has a different swatch painted. If you change the Source dropdown to a **different layer**, the fill boundary is taken from that layer instead.

**Example:** You have just finished painting all your region borders on the *Regions* layer. You now want to flood-fill the *Ground Types* layer. If you set **Source** to *Regions*, the Flood Fill will use your region shape as the fill boundary, so it stops precisely at each region edge — even if the Ground Types layer is still empty.

---

## 3. Tool Reference — Every Tool Explained

---

### Pan — Navigate the Map

**Hotkey:** `M`

```
 ┌───┐
 │ M │  ← Active when highlighted
 └───┘

 Cursor: ✥ (four-arrow move cursor)
 Left-click + drag → map scrolls in drag direction
```

#### What it does

Pan is the default navigation tool. It does **not paint anything**. It lets you scroll around the map by clicking and dragging, like dragging a document with your hand in a PDF reader.

When you open CAIME or load a project, **Pan is the default active tool** so you cannot accidentally paint anything before you choose the right layer and swatch.

#### How to use it

1. Click the **Pan tool icon** in the Toolbar, or press **M**.
2. Move your mouse over the map canvas.
3. **Click and hold** the left mouse button anywhere on the canvas.
4. **Drag** in any direction — the map moves with your cursor.
5. Release the mouse button when the area you want is centred in view.

> **Shortcut to pan without switching tools:** You can also pan at any time — even when a different tool is active — by pressing and holding the **middle mouse button** and dragging. This saves you from having to switch away from your current painting tool.

---

### Zoom — Zoom In and Out

**Hotkey:** `Z`

```
 ┌───┐
 │ Z │
 └───┘

 Mouse wheel scroll up   → Zoom in  (map gets larger)
 Mouse wheel scroll down → Zoom out (map gets smaller)
```

#### What it does

The Zoom tool changes how much of the map is visible in the canvas. Zoom in close to paint tiny details along a riverbank; zoom out to see the whole map at once and check region boundaries.

#### How to use it

1. Click the **Zoom tool icon** in the Toolbar, or press **Z**.
2. **Scroll the mouse wheel upward** to zoom in.
3. **Scroll the mouse wheel downward** to zoom out.

> **Shortcut to zoom without switching tools:** The mouse wheel zooms **at any time**, regardless of which tool is active. You do not need to switch to the Zoom tool just to zoom in or out — simply scroll the wheel while hovering over the map.

> **Tip:** When you are using the **Brush** tool, the brush cursor (the circle) automatically resizes itself as you zoom so that it always represents the correct number of hexes.

---

### Brush — Paint Individual Hexes

**Hotkey:** `B`

```
 ┌───┐
 │ B │
 └───┘

 Cursor: ○ (circle showing current brush area)
 Left-click        → paint a single spot
 Left-click + drag → paint a continuous stroke
```

#### What it does

The Brush is the main painting tool. Click a hex (or drag across many hexes) and it applies your **currently selected Swatch** from the **Swatches** panel to every hex you touch. Think of it like painting with a round brush in Photoshop — the brush size slider controls how big the tip is.

The circular cursor changes size based on both your **Brush Size** setting and your current **zoom level**, so the cursor always accurately shows which hexes will be painted.

#### How to use it

1. In the **Layers** panel (right sidebar), click the **radio button** next to the layer you want to paint on (e.g. *Ground Types*).
2. In the **Swatches** panel (right sidebar), open the dropdown and select the swatch you want to paint with (e.g. *Grassland*).
3. Click the **Brush tool icon** in the Toolbar, or press **B**.
4. Use the **Brush Size slider** in the Quick-Settings Bar to choose your brush diameter (1–10).
5. **Click** on the map canvas to paint a single area, or **click and drag** across hexes to paint a continuous stroke.
6. Release the mouse button when done.

```
Example: painting a grassland region

  Before painting:                   After painting (Brush size 3):
  ⬡ ⬡ ⬡ ⬡ ⬡ ⬡                     ⬡ ⬡ ⬡ ⬡ ⬡ ⬡
  ⬡ ⬡ ⬡ ⬡ ⬡ ⬡                     ⬡ [G][G][G] ⬡
  ⬡ ⬡ ⬡ ⬡ ⬡ ⬡   →→→ drag →→→      ⬡ [G][G][G] ⬡
  ⬡ ⬡ ⬡ ⬡ ⬡ ⬡                     ⬡ [G][G][G] ⬡
  ⬡ ⬡ ⬡ ⬡ ⬡ ⬡                     ⬡ ⬡ ⬡ ⬡ ⬡ ⬡
                                       [G] = Grassland swatch applied
```

> **Tip:** After painting a large area with a big brush, switch down to Brush Size 1 and clean up the edges before saving. Jagged coastlines and uneven region edges are much easier to fix while you are still in the painting session than after export.

> **Tip:** You can paint over already-painted hexes to change them — the new swatch replaces the old one. Use this to quickly correct individual miscoloured hexes without having to erase first.

---

### Flood Fill — Fill an Entire Area at Once

**Hotkey:** `F`

```
 ┌───┐
 │ F │
 └───┘

 Cursor: ◈ (fill cursor)
 Left-click once → fills the connected area
```

#### What it does

Flood Fill works exactly like the "paint bucket" in MS Paint. Click anywhere inside a connected area, and every hex in that area that has the **same current swatch** gets repainted with your **newly selected swatch** — all in one click.

It is the fastest way to paint large homogeneous areas (like painting an entire sea region a single climate, or colouring a whole region's terrain in one go).

The **Flood Fill Source** dropdown in the Quick-Settings Bar lets you define what counts as a "boundary" — see [Flood Fill Source Dropdown](#flood-fill-source-dropdown) above for details.

#### How to use it

1. Select your target **Layer** and desired **Swatch** from the right sidebar.
2. *(Optional)* Set the **Source** dropdown in the Quick-Settings Bar if you want to use a different layer's shape as the fill boundary.
3. Click the **Flood Fill tool icon** in the Toolbar, or press **F**.
4. **Click once** on any hex inside the area you want to fill.
5. CAIME immediately fills every connected hex that matches the boundary condition.

```
Example: filling a region with Temperate climate

  Before:                  After one click on the centre:
  · · · · · ·              · · · · · ·
  · · R · · ·              · · T · · ·     R = Region border (boundary layer)
  · · R · · ·   → click →  · · T · · ·     T = Temperate climate filled
  · · R · · ·              · · T · · ·
  · · · · · ·              · · · · · ·
```

> **Tip:** Flood Fill is almost always faster than brushing for filling large same-shaped areas. Use the Brush for precise edge detail and the Flood Fill for bulk coverage.

> **Warning:** If the fill seems to "leak" out of the intended area and spreads further than expected, check that the **Source** dropdown is set to the correct boundary layer. A wrong Source setting means the fill has no boundary to stop at.

---

### Eraser — Remove Paint from Hexes

**Hotkey:** `E`

```
 ┌───┐
 │ E │
 └───┘

 Cursor: ○ (circle, same as Brush)
 Left-click        → clear a single spot
 Left-click + drag → clear a continuous stroke
```

#### What it does

The Eraser works exactly like the Brush, but instead of applying a swatch it **removes** the swatch data from each hex it touches, returning those hexes to a blank/empty state. The **Brush Size slider** controls the eraser's area just like it does for the Brush.

Use the Eraser to clean up stray painted hexes, correct overspills along region edges, or clear an area you want to repaint differently.

#### How to use it

1. Select the **Layer** you want to erase data from in the **Layers** panel.
2. Click the **Eraser tool icon** in the Toolbar, or press **E**.
3. Adjust the **Brush Size slider** in the Quick-Settings Bar to match how much you need to erase at once.
4. **Click** or **click and drag** over the hexes you want to clear.

> **Tip:** If you erased too much, press **Ctrl+Z** immediately to undo. CAIME supports multiple undo steps, so you can step back through as many eraser strokes as needed.

> **Tip:** The Eraser and Brush share the same size slider — if you were brushing at size 8 and switch to the Eraser, it will start at size 8 as well. Reduce the size before erasing if you only need to clean up a thin edge.

---

### Background Image — Load a Reference Image

**Hotkey:** `I`

```
 ┌───┐
 │ I │
 └───┘

 Clicking this button opens a file picker — it is not a dragging tool.
 Accepted formats: .jpg  .jpeg  .bmp  .tiff  .png
```

#### What it does

The Background Image tool is unlike the other tools — clicking its icon in the Toolbar **immediately opens a file browser window** rather than activating a dragging cursor. Once you select an image file, CAIME places it as a semi-transparent layer *underneath* the hex grid in the viewport.

This is ideal for tracing over a reference map: scan a hand-drawn map, load it as a background image, and paint your hexes on top. The image is only a visual aid and is **not saved in the project file** — it does not affect any exported data.

After you select an image (or if you click Cancel in the file browser), CAIME automatically switches you back to whichever tool was previously active.

#### How to use it

**Loading a reference image:**

1. Click the **Background Image tool icon** in the Toolbar, or press **I**.
2. A **Windows file browser** window will open immediately.

   ```
   ┌─ Open ────────────────────────────────────────────────────┐
   │                                                            │
   │  Look in: [My Documents ▼]                                 │
   │  ┌─────────────────────────────────────────────────────┐  │
   │  │  reference_map.png                                  │  │
   │  │  campaign_sketch.jpg                                │  │
   │  └─────────────────────────────────────────────────────┘  │
   │  File name: [reference_map.png                  ]         │
   │  File type: [Image files (*.jpg, *.jpeg, *.bmp…)▼]       │
   │                                   [ Open ] [ Cancel ]     │
   └────────────────────────────────────────────────────────────┘
   ```

3. Navigate to your reference image file. Supported formats: **JPG, JPEG, BMP, TIFF, PNG**.
4. Select the file and click **Open**.
5. The image appears behind the hex grid in the map canvas.
6. CAIME automatically switches back to your previously active tool.

**Adjusting image visibility:**

7. Use the **Image Opacity slider** in the Quick-Settings Bar to set how strongly the reference image shows through (0 = invisible, 100 = fully visible).
8. A value of around 30–50 usually works well — the image is visible enough to trace but not so bright that it obscures your painted hexes.

**Removing the reference image:**

9. Click the **Clear background image button (✕)** in the Quick-Settings Bar.
10. The image disappears from the viewport. Your painted layers are unaffected.

> **Tip:** The image opacity slider is always available in the Quick-Settings Bar, even when you have a different tool selected. You can adjust it at any time without switching to the Background Image tool.

> **Tip:** If your reference image does not match the scale of your map, that is fine — CAIME stretches it to fill the entire canvas. Just use it as a rough guide for region shapes and road paths.

---

### Line — Paint a Straight Line of Hexes

**Hotkey:** `L`

```
 ┌───┐
 │ L │
 └───┘

 Step 1: Left-click to place the start point   (●)
 Step 2: Left-click again to place the end point and draw the line
         ●━━━━━━━━━━━━━━━━━━━━━━━━●
```

#### What it does

The Line tool lets you paint a perfectly straight path of hexes between two points you click. It is useful for painting road networks, river channels, or any other data that needs to follow a straight or diagonal path across the map. CAIME automatically calculates which hexes fall along the straight line between your two click points and paints all of them in one action.

#### How to use it

1. Select your target **Layer** and **Swatch** from the right sidebar.
2. Click the **Line tool icon** in the Toolbar, or press **L**.
3. **Click once** on the map canvas to set the **start point** of the line.
4. Move your cursor to where you want the line to end.
5. **Click once more** to set the **end point** — CAIME immediately paints all the hexes along the straight path between your two clicks.

```
Example: painting a road across a valley

 Click start here         Click end here
      ●                         ●
       ↘                       ↗
        [R][R][R][R][R][R][R][R]
           Road hexes painted automatically
```

> **Tip:** For long diagonal roads and rivers, the Line tool saves significant time compared to brushing freehand. For slight curves, use a series of short Line tool strokes that change direction gradually.

> **Tip:** If you make a mistake, press **Ctrl+Z** to undo the entire line in a single step — you do not need to erase each painted hex individually.

---

### Color Picker — Sample a Colour from the Canvas

**Hotkey:** `P`

```
 ┌───┐
 │ P │
 └───┘

 Cursor: ✦ (eyedropper cursor)
 Move the cursor over a hex → the Swatches panel updates in real time
```

#### What it does

The Color Picker works like the eyedropper in Photoshop. Move it across the map canvas and it reads the swatch value of whatever hex is underneath your cursor, instantly setting that swatch as your active painting colour in the **Swatches** panel. This means you can "copy" the swatch from one hex and then immediately use the Brush, Flood Fill, or Line tool to apply the same value elsewhere.

Unlike the other tools, the Color Picker updates the selected swatch **as you move** your cursor across the map (you do not have to click to sample).

#### How to use it

1. Click the **Color Picker tool icon** in the Toolbar, or press **P**.
2. Move your cursor over the map canvas. As you hover over different hexes, the **Swatches panel** dropdown updates in real time to show the swatch name of the hex beneath your cursor.
3. When the Swatches panel shows the swatch you want, switch to another tool (e.g. press **B** for Brush) to begin painting with that sampled swatch.

**One-step sampling with the Alt key:**

You do not need to switch to the Color Picker tool and back again. While any painting tool is active:

1. **Hold Alt** on your keyboard.
2. The cursor changes to the Color Picker eyedropper.
3. Move your cursor over the hex whose swatch you want to copy.
4. **Release Alt** — the sampled swatch is now active, and you are back to your previous tool, ready to paint.

```
Workflow example using Alt to sample without switching tools:

  1. You have the Brush tool active, painting Grassland.
  2. You notice a hex across the map has the exact climate swatch you need.
  3. Hold Alt → cursor becomes eyedropper → hover over that hex.
  4. Release Alt → you are back to the Brush, now painting with the sampled swatch.
```

> **Tip:** The Color Picker is especially useful when you are not sure which swatch name is painted on a region. Hover over any hex to instantly identify what swatch it has without having to zoom in and guess.

> **Tip:** Use the Alt key shortcut constantly — it is the fastest way to match swatches across distant parts of the map. Professional CAIME users rarely switch to the Color Picker tool manually; they hold Alt instead.

---

## 4. Keyboard Shortcuts — All Eight Tools at a Glance

```
┌──────────┬──────────────────────┬────────────────────────────────────────────┐
│ Hotkey   │ Tool                 │ What It Does                               │
├──────────┼──────────────────────┼────────────────────────────────────────────┤
│    M     │ Pan                  │ Click-drag to scroll around the map        │
│    Z     │ Zoom                 │ Mouse wheel to zoom in/out                 │
│    B     │ Brush                │ Click/drag to paint hexes with swatch      │
│    F     │ Flood Fill           │ Single click to fill a connected area      │
│    E     │ Eraser               │ Click/drag to clear painted data           │
│    I     │ Background Image     │ Opens file browser to load reference image │
│    L     │ Line                 │ Click start then end to paint a straight   │
│          │                      │ line of hexes                              │
│    P     │ Color Picker         │ Move over hex to sample its swatch         │
├──────────┼──────────────────────┼────────────────────────────────────────────┤
│ Hold Alt │ Temporary Color Pick │ Sample a swatch without switching tools    │
│ Mid-drag │ Temporary Pan        │ Pan the map without switching tools        │
│ Scroll ↑ │ Zoom in              │ Zoom in at any time, any tool              │
│ Scroll ↓ │ Zoom out             │ Zoom out at any time, any tool             │
└──────────┴──────────────────────┴────────────────────────────────────────────┘
```

---

## 5. Pro-Tips & Troubleshooting

- **My Brush or Eraser is not painting anything.**
  Check three things in order: (1) Is a **Layer** selected? Open the Layers panel on the right sidebar and click the radio button next to the layer you want to paint. (2) Is a **Swatch** selected? Open the Swatches dropdown and pick a swatch. (3) Is the toolbar enabled? Some menu operations temporarily lock the toolbar — check if the toolbar icons appear faded/grey, which means another operation is in progress.

- **Flood Fill spread to places I did not intend.**
  This happens when the **Source** dropdown in the Quick-Settings Bar is set to a layer with no boundaries where you expected them. Check that you have set **Source** to the layer whose shape you want to use as the fill boundary (for example, the *Regions* layer if you want to fill within a region outline). If the Source layer itself has no data painted there yet, the fill has no walls to stop it and will spread across the entire map.

- **The Background Image tool opened a file browser, I clicked Cancel, and now my tool has changed.**
  This is normal. The Background Image tool is a one-click action, not a persistent tool. After you either load an image or cancel, CAIME automatically restores whichever tool was previously active. Simply click the Brush, Line, or any other tool to return to normal painting.

- **The Line tool drew a line in completely the wrong place.**
  Press **Ctrl+Z** immediately to undo the entire line in one step. This is faster than erasing it hex by hex. Then zoom in to carefully confirm your start point before clicking again.

- **When I hold Alt to use the Color Picker, nothing seems to happen.**
  Make sure a project is open and the main map canvas is the focused window. The Alt key shortcut only works while your cursor is hovering directly over the map canvas area. If you hover over the sidebar or the menu bar while holding Alt, the temporary color picker will not activate.

- **The Brush cursor circle does not match the size I expected.**
  The brush cursor scales with both the **Brush Size slider** and the current **zoom level**, so it always shows the correct real-world area on screen. If the circle looks too large, zoom in — the hexes will get bigger and the circle will look smaller relative to them. If the circle looks too small, zoom out.

---

*Guide version: 1.0 — Campaign AI Map Editor*
