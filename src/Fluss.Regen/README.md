# Fluss.Regen

Regen is the "Repetitive Event-sourcing code-GENerator", and removes the need for assembly scanning to register
resources like

- Upcasters
- Validators
- SideEffects
- Policies

It also adds support for cached selector-functions, marked by the `[Selector]` attribute.

## Usage

To register all components listed above detected by Regen, call the generated `.Add*ESComponents()` function on your
`IServiceCollection`. The name depends on the name of your assembly.

### Selector

Marking a `static` function with `[Selector]` creates an extension method on `IUnitOfWork` which adds an easy way of
caching in the context of an `IUnitOfWork`, returning known, previously computed data if available.
