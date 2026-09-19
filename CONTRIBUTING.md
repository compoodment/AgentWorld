# Contributing to AgentWorld

AgentWorld has frozen its first-world concept and is preparing a deterministic
vertical slice. Design work remains welcome when it is grounded in a concrete
prototype finding or changes an explicit decision.

## Useful contributions right now

- build a small, reproducible feasibility prototype
- challenge an implementation choice with evidence
- identify a missing invariant, failure mode, or acceptance test
- propose a simpler or safer model for a demonstrated prototype need
- improve the documentation or diagrams
- compare engine, storage, networking, or sandboxing options

## Design contribution rules

1. Read the relevant document before proposing a change.
2. Separate a requirement from an implementation preference.
3. Record meaningful trade-offs and unresolved questions.
4. Do not turn a speculative idea into a fixed requirement without a decision
   or prototype evidence.
5. Keep the protected simulation kernel smaller than the moddable world around
   it.
6. Never add API keys, private world saves, or other secrets to the repository.

Issues and pull requests should say whether they are changing the concept,
testing feasibility, or implementing an already-agreed design.

For delivery work, GitHub issues and milestones own the changing task state.
Every implementation pull request should link its issue, governing contract or
decision, and reproducible verification evidence. Update the implementation
ledger only when merged evidence changes a capability's status; do not duplicate
daily GitHub activity in design documents.
