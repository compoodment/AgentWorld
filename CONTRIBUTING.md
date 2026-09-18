# Contributing to AgentWorld

AgentWorld is currently in the concept and foundation-design phase. Design
work is welcome before implementation work.

## Useful contributions right now

- challenge an assumption in the design documents
- propose a simpler or safer model
- identify a missing invariant or failure mode
- build a small isolated feasibility prototype
- improve the documentation or diagrams
- compare engine, storage, networking, or sandboxing options

## Design contribution rules

1. Read the relevant document before proposing a change.
2. Separate a requirement from an implementation preference.
3. Record meaningful trade-offs and unresolved questions.
4. Do not turn a speculative idea into a fixed requirement without discussion.
5. Keep the protected simulation kernel smaller than the moddable world around
   it.
6. Never add API keys, private world saves, or other secrets to the repository.

Issues and pull requests should say whether they are changing the concept,
testing feasibility, or implementing an already-agreed design.
