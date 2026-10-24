# ExamGuardian documentation site

Astro/MDX documentation for the actual system, with task-oriented entry points:

- `/`: purpose, attempt lifecycle, and routes into the documentation.
- `/start`: dev/production setup, native client, operations, and troubleshooting.
- `/guide`: examiner workflow from participant setup to review.
- `/dev`: service responsibilities and data flow.
- `/docs`: documentation map, component reference, and validation boundaries.

## Work locally

The repository launcher `./run.ps1` starts documentation as part of the dev environment. Use the printed docs URL; the port is saved in `.env.dev`. Source edits reload automatically.

For documentation-only work, use Node satisfying `package.json` (the Docker image uses Node 24). From this directory:

```powershell
npm.cmd ci
npm.cmd run dev -- --background
npm.cmd run astro -- dev status
npm.cmd run astro -- dev stop
```

## Validate

```powershell
npm.cmd run build
npm.cmd run check:diagrams
```

The static output is `dist/`. Check new routes, internal links/anchors, and narrow-screen layout before publishing. Production application startup does not serve this documentation site.

Navigation groups live in `src/data/navigation.ts`; both the documentation map and sidebar use them. Use `DocsLayout.astro` for reference and task pages, and `Layout.astro` for the overview. Preserve public routes when moving content.

Confirm operational commands against `run.ps1`, `scripts/Start-Environment.ps1`, and the Compose overlays. Keep current behavior separate from planned features and recorded validation. Do not document fixed local ports or secrets from a workstation's environment files.
