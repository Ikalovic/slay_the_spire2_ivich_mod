# Ivich Foundation Implementation Plan

> Execute using subagent-driven-development with independent file ownership, then review shared integration.

**Goal:** Build the first real Ivich mod development package against the installed game while artwork is being prepared.

**Architecture:** Pure C# rules in `src/Ivich.Core`, actual BaseLib models in `src/Ivich.Mod`, source-traceable card catalog in `content`. Game DLLs stay external. Build never installs automatically.

**Tech Stack:** .NET 9, game 0.107.1, BaseLib 3.4.7, GodotSharp from installed game.

- [x] Extract 82 card definitions from current design, preserving upgrades, costs, tags, and source hash. Validate counts and unique IDs; record actual enabled IDs separately.
- [x] Write and run rule regression tests before implementation: wrong-form resources rejected, no turn-one double mana, dragon odd HP remainder, atomic mixed costs, distance/encouragement rounding, freezing threshold doubling, doom baseline, exact health ratio, immutable casting snapshots and queue drain.
- [x] Implement `Ivich.Core` with no game dependency. Run `dotnet run --project tests/Ivich.Core.Tests`.
- [x] Create `Ivich.Mod.csproj` with configurable local DLL paths; add entrypoint, character and pools, starter relic, powers and combat lifecycle. Verify `dotnet build src/Ivich.Mod` against actual assemblies.
- [x] Implement B01–B06, C01–C28, U01–U02, T01 with explicit costs, upgrade effects and targeting. U01/U02 provide legal power cards for the mandatory merchant power slot in each form. Unimplemented designs remain outside rewards.
- [x] Implement rest advancement and saved run progress. Verify legitimate spending/loss only; keep permanent state separate from combat state.
- [x] Add placeholder asset contract and import helper, local environment example, build/package scripts and Chinese README.
- [x] Review enabled features against design, run automated tests and real build, produce checksummed release archive, record game validation still required.
