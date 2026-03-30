# Changesets

Create a changeset when a merged change should affect the next stable npm release.

- Run `npm run changeset` in `mcp-server`.
- Commit the generated markdown file under `.changeset/`.
- `main` pushes publish snapshot beta packages.
- GitHub releases publish stable versions after applying pending changesets.