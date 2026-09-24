# ADR-0001 — One spec, two implementations, one contract

**Status:** accepted

## Context
The same lending domain is implemented in Python
([spec-driven-ddd-python](https://github.com/hasanozkan/spec-driven-ddd-python))
and here in C#. The specs claim to be the source of truth; two implementations
are how that claim gets tested.

## Decision
- `specs/` is byte-identical to the Python repository's; CI (`spec-mirror`)
  fails on any difference. The specs are written language-neutrally.
- Rule ids are proven the same way in both: a test tagged with the id
  (`[Trait("Rule", "LEND-R5")]` here, a pytest marker there), and a gate that
  fails when a rule has no test or a test cites no rule.
- The HTTP contract is the same: snake_case JSON, the same paths, the same
  problem codes. CI (`conformance`) runs the Python implementation's smoke test,
  unchanged, against this API.

## Consequences
A spec change is made once and must land in both implementations — the
mirror job makes forgetting impossible. What differs is only what each
platform does best (see ADR-0002).
