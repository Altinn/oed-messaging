# CLAUDE.md

Guidance for Claude Code (claude.ai/code) in this repository.

## Coding Conventions

- **Backend**: `var` for local variables, expression-bodied members where concise, PascalCase for types/methods/properties, camelCase for parameters/locals, _underscore prefix for private fields. Collections initialized with collection initializers, where readable. Use `async/await` for async code. Use `record` types for immutable data models where appropriate. Never use ConfigureAwait on async code (the NSwag-generated client is exempt).
- **Language version**: `Altinn.Dd.Correspondence` targets `net10.0;net9.0;net8.0` and sets no `<LangVersion>`, so each target compiles with its default (C# 12 on `net8.0`). A language feature newer than C# 12 breaks the `net8.0` build of that library.
- **Testing**: xUnit, NSubstitute, `RichardSzalay.MockHttp`.

## Source control
- When committing, always include a **meaningful commit message**. If the message exceeds one line, use a short description in the first line, then a blank line, then a more detailed explanation. 
- Use git rebase to squash multiple commits into one before merging a feature branch. 
- Prefer trunk-based development, don't use feature branches and PRs if not necessary. 
- Never ever discard any changes in the working tree without first committing or stashing them. Use `git stash` to temporarily save changes if you need to switch branches.
- Avoid merge commits at all costs, prefer rebasing or fast-forward merges.
- Keep feature branches current by rebasing them onto `main` (`git rebase main`), never by merging `main` in. A rebased branch that is already pushed needs `git push --force-with-lease`, never `--force`, and check with the branch's other authors before rewriting it.

## Tests

**Verify your own tests by making them fail.** A green test proves nothing until you've seen it go red for the right reason. After writing a test, temporarily mutate the production code it covers (e.g. disable the guard, force a condition to `false`, skip the call) and confirm *that specific test* fails — then revert. Watch especially for tests asserting only `DidNotReceive` — they pass vacuously if the code path is never reached.

**Build `DateTimeOffset` fixtures with a non-zero offset** (e.g. `TimeSpan.FromHours(2)`). With `TimeSpan.Zero`, `.UtcDateTime` and `.DateTime` are identical, so the test cannot fail on an offset-shifting bug.

Configure fakes to **honor their arguments** (e.g. return values filtered by the requested ids) — a fake that ignores args can mis-drive the real collaborators it feeds. For a **generic** method the matcher's type argument picks the closed method: `Arg.Any<object>()` binds to `Method<object>` and never matches a real `Method<Concrete>` call, so a `DidNotReceive` on it passes vacuously. Match the concrete type, and match the overload's arity — a short overload implemented as a default interface method is a separate method to the fake.

## Azure Key Vault

Never read, list or enumerate Azure Key Vault secrets — no `az keyvault secret show`/`list`, no `SecretClient`, no portal scraping, and not even secret names or value lengths. If a task needs a secret value, ask the user to supply it or to run the command themselves.
