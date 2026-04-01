---
name: solidworksmodeling
description: Use for SolidWorks MCP part and assembly work such as interference fixes, topology selection, face-hosted sketches, extrudes, cuts, mates, saves, and verification in parent assemblies.
---

# SolidWorks Modeling Skill

Use this skill when the task involves editing a SolidWorks part or assembly through MCP tools, especially for interference fixes, topology-driven selection, sketch-based feature work, or assembly verification.

## Core Rules

- Confirm the active document first. Do not assume the visible window is the part you intend to edit.
- Treat SolidWorks as a selection-first UI. Use the FeatureManager tree to identify the real target object, then use commands or MCP tools against that confirmed target.
- For assembly interference issues, identify the actual component file paths before changing geometry.
- Before any sketch-based operation, clear the current selection, select a real planar face on the target solid, and only then start the sketch.
- Do not create sketches in free space when the intent is to modify model geometry. Face-hosted sketches are materially more stable.
- Before reading the FeatureManager tree or deleting features or loose sketches, call `GetEditState` first. If `IsEditing` is true, finish the active sketch before `ListFeatureTree`, `DeleteFeatureByName`, or `DeleteUnusedSketches`.
- If a face-based cut or boss fails, verify the feature direction. A top-face relief cut often needs the direction flipped so the feature goes into the body.
- In localized SolidWorks environments, do not rely on English plane labels. Prefer semantic plane resolution or topology-driven selection.
- Preserve functional geometry unless the user explicitly requests otherwise. For pulley brackets, keep pulley hole size and pulley center position unchanged while adjusting only clearance material.
- Prefer the smallest edit that removes the issue, such as a shallow relief cut or reducing a non-functional top surface.
- Save the edited part before reopening or checking the parent assembly.
- After saving, verify the result in the assembly and confirm there is visible clearance rather than coplanar overlap.
- When named selection is unreliable, inspect topology first and select faces or edges by index.
- If document or assembly queries suddenly start failing with `0x800706BA`, assume the cached COM session may be stale and verify Hub health before editing geometry.

## Recommended Workflow

1. Connect to SolidWorks and confirm the active document.
2. If connection behavior looks suspicious, validate `get_active_document`, `list_documents`, and `list_components` before attempting geometry edits.
3. Find the interfering components and resolve the concrete part file that should be edited.
4. Use the FeatureManager tree or assembly component list to confirm the exact component name, hierarchy, and path.
5. Open the target part as the active document.
6. If you need tree inspection or cleanup, call `GetEditState` and ensure the document is not in sketch edit mode first.
7. Inspect available faces and choose the host face for the edit.
8. Create the sketch on that selected face.
9. Apply the minimal boss or cut needed to resolve the issue.
10. Finish the sketch before any later tree read or cleanup step.
11. Save the part.
12. Reopen or refresh the parent assembly and verify the interference is gone.

## UI Guidance

- CommandManager is the context-sensitive command surface. Match the current document state before assuming which operation should be available.
- FeatureManager is the authoritative structure view for features, sketches, components, reference geometry, and parent/child relationships.
- In MCP-driven workflows, FeatureManager reads and tree-based deletes are only valid in non-edit state, so exit sketch edit before using them.
- PropertyManager is for the parameters of the current command, not for understanding overall model structure.
- In large assemblies, prefer tree-driven identification over viewport-driven guessing.
- When debugging rebuild or selection issues, inspect the tree for suppressed items, reordered features, reference geometry, and parent/child dependencies.

## Interference Fix Guidance

- If two planes should not coincide, prefer creating a small positive clearance instead of leaving them mathematically coplanar.
- When the interfering part contains a pulley, bearing, shaft hole, or similar functional feature, avoid moving that feature unless the task explicitly allows it.
- For bracket-height fixes, first consider trimming the non-functional top material rather than relocating the working hole.

## Example Pattern

Scenario: an x-axis pulley fixing bracket interferes with the underside of a 2020 aluminum plate.

1. Open the bracket part.
2. Select the bracket top face.
3. Start a sketch on that face.
4. Sketch a rectangle covering the top material that can be relieved.
5. Perform a shallow cut into the solid, keeping the pulley hole geometry unchanged.
6. Save the part and verify in the assembly that the bracket top face is now below the plate bottom face.