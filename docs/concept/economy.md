---
title: Economy, Inventories, and Exchange
type: concept
status: frozen
updated: 2026-09-19
---

# Economy, Inventories, and Exchange

The exact deterministic transaction, reservation, resource, and lot/freshness
semantics are specified in the
[deterministic kernel contract](../planning/deterministic-kernel-contract.md).

The economy is how survival becomes social. A farmer should be able to grow
food, own or share it, and decide what to do with it. Other inhabitants should
need to negotiate, work, trade, cooperate, steal, migrate, or build competing
farms rather than receiving invisible free rations.

The kernel protects truthful accounting. It does not impose one economic
ideology.

## Inventories are first-class state

Inventories may belong to:

- an individual inhabitant
- a household or family
- a farm, workshop, store, or other building
- a communal stockpile
- a guild, company, cooperative, or government
- a market, warehouse, or travelling caravan

An inventory entry needs enough identity to answer at least:

- what exists and in what quantity
- where it is
- who owns it, if anyone
- its condition, freshness, quality, or reservations
- whether it is available for use, sale, work, or a contract

The inventory system must prevent duplication, negative quantities, spending the
same item twice, and transferring something that the actor cannot access.

## Exchange is an atomic action

A trade is a proposal, not a direct edit:

```text
observe holdings and offer
  → propose an exchange, gift, wage, or contract
  → validate ownership, quantities, access, and local rules
  → commit all sides atomically
  → emit a transaction and social/economic events
```

If one side fails validation, nothing is half-transferred. The event history
should record successful exchanges and meaningful failures without exposing
private thoughts unnecessarily.

The same ledger can support gifts, barter, wages, taxes, rents, debt payments,
and communal distribution. Those are world rules layered over authoritative
ownership and transfer facts.

## No mandatory economic ideology

The kernel should not require universal money. A world may develop:

- barter
- commodity money
- minted currency or local tokens
- labour credits
- reputation-backed exchange
- debt and contracts
- gift economies
- communal allocation
- mixtures that vary by settlement or institution

For the first prototype, inventories, ownership, and direct transfer matter
more than a complete banking system. A simple exchange medium can be added for
testing, but it must remain replaceable rather than becoming a hidden kernel
law.

## The farmer example

A farm produces food into an inventory. Its owner, household, cooperative, or
workers may then eat it, reserve it, sell it, barter it, gift it, pay wages with
it, or use it to seed the next harvest.

If one farmer controls most of the food and charges heavily, the simulation
should allow the consequences to unfold:

- competitors build farms or improve yields
- buyers bargain or form a cooperative
- workers demand wages or leave
- the farmer gains influence or enemies
- people migrate, steal, revolt, tax, or create welfare rules
- shortages cause hunger if nobody solves the problem

The game should not secretly neutralize a monopoly. It should also not force a
monopoly to be evil; stockpiling for winter or charging enough to maintain a
farm can be rational and socially useful.

## Deterministic ledger, agent decisions

The simulation handles:

- quantities and locations
- production and consumption
- prices as stored world facts
- atomic settlement of trades
- contract deadlines and payment failures
- spoilage, loss, and other declared item effects

An LLM handles meaningful choices such as whether to sell now, stockpile for a
forecast shortage, lower prices to gain allies, hire labour, join a cooperative,
or attempt to replace a supplier. Routine transactions do not need a model
call.

## Abundance is allowed; forged state is not

An inhabitant may create a highly productive crop through a valid mod proposal.
It cannot directly write `hunger = 0` or mint an infinite inventory. A crop must
declare its inputs, growth conditions, outputs, and effects, and pass the same
resource, time, and compatibility checks as any other world system.

If a world legitimately discovers near-infinite food, that is a civilization-
changing event. Scarcity, labour, prices, migration, ecology, and politics can
change around it. The kernel protects causality, not permanent difficulty.

## Deliberate staging

1. Inventories, locations, ownership, consumption, and atomic transfer.
2. Food production, storage, spoilage, and direct barter.
3. Local prices, wages, simple contracts, and organizations.
4. Currency, credit, taxation, law, and more complex institutions where play
   demonstrates that they are needed.

The first stage is foundational. The later stages remain world rules, not a
promise that every world must become a financial simulation.
