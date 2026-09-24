# ADR-0002 — Assemblies as boundaries, tests for the rest

**Status:** accepted

## Decision
Each bounded context is its own project: `Library.Catalog`, `Library.Lending`,
both referencing only `Library.Contracts`. With no reference between them, a
cross-context call does not compile.

The compiler only enforces this while the reference is absent — an unused
`ProjectReference` compiles and opens the door — so a test reads the project
files. Inside a context, where the compiler cannot help, ArchUnitNET tests
hold the layers: the domain depends on no framework and no outer layer; the
application reaches neither the API nor the infrastructure.

## Found by breaking it
The project-file test exists because the first negative control — adding the
reference without using it — passed every other check.
