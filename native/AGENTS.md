# Native project scope

Current work is limited to `native/`, focused on the PDF signer and the C signer
component intended for integration with it. The repository's .NET and frontend
projects are outside the current scope unless the user explicitly expands it.
Continue to follow any more specific `AGENTS.md` instructions within this tree.

## Treeclimber navigation

- Agents must use the `treeclimber` CLI as their first navigation point for
  this project. Start code exploration with a Treeclimber map, then use focused
  queries to locate relevant files and symbols. Ordinary source reads and
  searches may follow for implementation details or when Treeclimber is blocked.
- Keep queries scoped to `native/` or the relevant component within it.
- Do not climb vendored code, third-party dependencies, dependency caches, or
  generated build output. Use query `omit` rules to remove these trees from
  scope entirely; do not rely on the default profile or `exclude` landmarks.
  Cover both root-level and nested directories (for example, `build/**` and
  `**/build/**`, `vendor/**` and `**/vendor/**`, `third_party/**` and
  `**/third_party/**`, `vcpkg_installed/**` and `**/vcpkg_installed/**`).
  Add equivalent rules for other dependency or output paths encountered in
  the component. Keep navigation focused on project-owned source.
- For CLI usage or implementation questions, consult the local Treeclimber
  source at `/home/jimmy/treeclimber` on WSL (the installed source location).
  Its `README.md` documents query syntax and examples.
- Treeclimber is in active development. Record observed issues, thoughts,
  suggestions, and successes in `native/treeclimber_feedback.md` after using
  it. Include the task context, command/query where useful, observed outcome,
  and actionable suggestions. Distinguish observations from hypotheses.
- `native/treeclimber_feedback.md` is the single shared feedback log at the
  native project root. It is append-only: never rewrite, delete, reorder, or
  replace existing entries. Append corrections as new entries. Do not create
  additional feedback logs in component directories or the outer repository.
