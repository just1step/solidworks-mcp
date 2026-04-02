---
name: solidworksmodeling
description: Use for SolidWorks MCP part and assembly work such as interference fixes, topology selection, face-hosted sketches, projected-edge face cuts, extrudes, cuts, mates, saves, and verification in parent assemblies.
---

# SolidWorks Modeling Skill

Use this skill when the task involves editing a SolidWorks part or assembly through MCP tools, especially for interference fixes, topology-driven selection, sketch-based feature work, or assembly verification.

## Core Rules

- Confirm the active document first. Do not assume the visible window is the part you intend to edit.
- Opening a document is not the same as activating it. If a document is already open, explicitly confirm that it became the active document before reading components, replacing parts, or editing geometry.
- Treat SolidWorks as a selection-first UI. Use the FeatureManager tree to identify the real target object, then use commands or MCP tools against that confirmed target.
- For assembly interference issues, identify the actual component file paths before changing geometry.
- In large or nested assemblies, do not stop at the top-level component list. Prefer `ListComponentsRecursive` to locate the concrete part file that owns the interfering geometry.
- Before any sketch-based operation, clear the current selection, select a real planar face on the target solid, and only then start the sketch.
- Do not create sketches in free space when the intent is to modify model geometry. Face-hosted sketches are materially more stable.
- When reasoning about SolidWorks APIs, prefer official API names and official preconditions. Do not invent wrapper-style method names and then reason from those invented semantics.
- Before reading the FeatureManager tree or deleting features or loose sketches, call `GetEditState` first. If `IsEditing` is true, finish the active sketch before `ListFeatureTree`, `DeleteFeatureByName`, or `DeleteUnusedSketches`.
- If a face-based cut or boss fails, verify the feature direction. A top-face relief cut often needs the direction flipped so the feature goes into the body.
- In localized SolidWorks environments, do not rely on English plane labels. Prefer semantic plane resolution or topology-driven selection.
- If the target part is reused in the assembly, first determine how many instances reference that same part file and explicitly tell the user which other placements will also change before editing the geometry.
- If only one reused placement needs to change, prefer replacing that single instance with a separate part file instead of editing the shared source part.
- `ReplaceComponents2` only replaces top-level components in the active assembly. For nested content, open the owning subassembly, activate it, replace the child there, save, then return to the parent assembly for verification.
- Preserve functional geometry unless the user explicitly requests otherwise. For pulley brackets, keep pulley hole size and pulley center position unchanged while adjusting only clearance material.
- Prefer the smallest edit that removes the issue, such as a shallow relief cut or reducing a non-functional top surface.
- Treat extrude, cut, revolve, shell, and other shape-changing feature edits as high-risk operations. After each such edit, export and inspect at least front, top, and right views before trusting the result.
- Never rely on a single view after a high-risk geometry edit. If the shape still looks ambiguous, also inspect an isometric view.
- If the three-view check shows an unexpected silhouette, floating geometry, or any unclear result, stop and show the views to the user before continuing with more edits.
- Save the edited part before reopening or checking the parent assembly.
- After saving, verify the result in the assembly and confirm there is visible clearance rather than coplanar overlap.
- After any replacement-based fix, rerun a directed interference check against the intended target pair. An alternate part name or file does not prove that clearance now exists.
- When named selection is unreliable, inspect topology first and select faces or edges by index.
- If document or assembly queries suddenly start failing with `0x800706BA`, assume the cached COM session may be stale and verify Hub health before editing geometry.

## Recommended Workflow

1. Connect to SolidWorks and confirm the active document.
2. If connection behavior looks suspicious, validate `get_active_document`, `list_documents`, and `list_components` before attempting geometry edits.
3. Find the interfering components and use `ListComponentsRecursive` to resolve the concrete part file that should be edited.
4. Use the returned hierarchy paths to confirm the exact component name, nesting, and path instead of assuming the target is top-level.
5. If that part file is reused in multiple placements, count the affected instances from the recursive list first and tell the user the downstream impact of changing the shared part.
6. Decide whether the fix should modify shared geometry or replace only one instance with a separate file.
7. If replacing only one nested instance, open the owning subassembly, confirm it is active, and perform the replacement there.
8. If editing geometry, open the target part as the active document.
9. If you need tree inspection or cleanup, call `GetEditState` and ensure the document is not in sketch edit mode first.
10. Inspect available faces and choose the host face for the edit.
11. Create the sketch on that selected face.
12. Apply the minimal boss or cut needed to resolve the issue.
13. Immediately export and inspect front, top, and right views of the changed part. Add an isometric export whenever the result is not obvious from three views alone.
14. If the geometry still looks questionable, show those views to the user and get confirmation before making another high-risk edit.
15. Finish the sketch before any later tree read or cleanup step.
16. Save the edited part or subassembly.
17. Reopen or reactivate the parent assembly and run a directed interference check on the original target pair before concluding the issue is fixed.

## Face-Outline Projection Cuts

- `ISketchManager.SketchUseEdge3` requires an active sketch and a selected set of edges or loops. Selecting only a face is not enough.
- For the common "cut from an existing planar face outline" workflow, prefer the high-level MCP tool `CutFaceByProjectedEdges` instead of manually chaining low-level tools.
- If you must execute the workflow manually, use the proven sequence exactly: select planar face -> open sketch on that face -> call `IFace2.GetEdges` -> select the returned edges -> call `SketchUseEdge3` -> exit sketch edit mode -> reselect the sketch feature -> call `FeatureCut4`.
- If the projected sketch looks correct but `FeatureCut4` still fails, inspect sketch edit state and whether the sketch feature is actually reselected before assuming the profile itself is invalid.

## Sketch Stability And Design Intent

- Prefer several small, stable sketches and features over one large sketch that carries unrelated geometry.
- Fully define important sketch geometry with dimensions and relations so later edits do not drift.
- Avoid daisy-chained dependencies where A drives B and B drives C. Prefer A driving both B and C directly.
- For symmetric geometry, prefer mirror workflows or mid-plane driven layouts so both sides stay consistent after changes.

## Symmetry And Reference Geometry

- Use Mirror Entities when the source sketch geometry already exists and you want a controlled post-draw mirror.
- Use Dynamic Mirror when the geometry should be created symmetrically from the start around a selected centerline or edge.
- Use offset planes for nearby secondary cuts, local bosses, or sketches that should not live on the original face.
- Use angle planes when a feature must be oriented from a face plus a rotational reference such as an edge or sketch line.
- Use mid planes for symmetric features, symmetric edits, and stable mirror references.
- Use cylindrical-surface-related planes when cylindrical positioning or cuts need a robust reference frame.

## Feature Ordering And Rounds

- Keep the primary shape stable first, then add broad fillets or chamfers later.
- If fillets need repair or coordinated radius edits, prefer FilletXpert-style workflows over manually reworking many related fillets one by one.

## External Reference Management

- Top-down references are acceptable during early assembly-driven design, but once the design stabilizes, prefer replacing external references with local dimensions and relations where practical.
- Do not break references blindly. Prefer replacing sketch planes, replacing relations, and re-fully-defining geometry when possible.
- Avoid circular references by anchoring external references to stable key components rather than chaining dependencies across multiple downstream parts.
- Avoid cross-hierarchy reference loops between top-level assemblies and deep subassembly content.
- Be cautious when adding new external references to features that already depend on external references.
- Be cautious with assembly-level cuts, hole features, patterns, and similar operations that can hide complex external dependencies.

## Broken Reference Recovery

- Use Find References first to identify what is missing and which path SolidWorks expects.
- For reference repair workflows, prefer repointing missing files during open before saving the document again.
- Replace like with like: parts with parts, and subassemblies with subassemblies.

## MCP Tooling Notes

- If named plane selection is unreliable, prefer topology-driven selection or use `PLANE` as the safer fallback selection type.
- Ensure the sketch profile is closed before extrude, cut, or revolve operations. Open contours should be treated as a hard blocker.
- After `FinishSketch`, still expect profile validation before extrusion or cut creation. Do not assume sketch exit implies a valid feature profile.
- For face-outline cuts, do not expose or prefer ad-hoc low-level tool chains when a proven one-shot workflow tool exists. Prefer the stable MCP workflow surface so other models reuse the same sequencing.
- Use `sw.assembly.replace_component` only when the target instance is top-level in the active assembly. Resolve the hierarchy with `ListComponentsRecursive` first so the replacement is applied in the correct assembly context.

## UI Guidance

- CommandManager is the context-sensitive command surface. Match the current document state before assuming which operation should be available.
- FeatureManager is the authoritative structure view for features, sketches, components, reference geometry, and parent/child relationships.
- In MCP-driven workflows, FeatureManager reads and tree-based deletes are only valid in non-edit state, so exit sketch edit before using them.
- PropertyManager is for the parameters of the current command, not for understanding overall model structure.
- In large assemblies, prefer tree-driven identification over viewport-driven guessing.
- When debugging rebuild or selection issues, inspect the tree for suppressed items, reordered features, reference geometry, and parent/child dependencies.
- Rename important features, sketches, planes, and components when structure clarity matters, because tree readability directly affects repair speed.
- Check parent-child relationships and rebuild order before reordering features. Do not drag features casually without confirming design intent.
- Standard tree folders such as planes, origin, bodies, equations, sensors, and annotations often reveal root causes faster than viewport inspection.

## Verification Gates

- High-risk geometry edits require a post-edit view gate: export front, top, and right views every time.
- If those three views are insufficient to explain the result, export an isometric view as a fourth check.
- Do not continue stacking more cuts, bosses, or other major edits on top of an unverified shape.
- If the post-edit views do not clearly match design intent, pause and ask the user to confirm before proceeding.

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