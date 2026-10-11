# Threadlink Framework

![Powered by Threadlink. Weaving worlds together.](.gitbook/assets/threadlink-banner.png)

**Threadlink** is a modular runtime framework layered over Unity: a self-deploying core, native subsystems for events, time, scenes, input and UI, audio and platform services, a type-safe identifier system, a designer-facing data container, and a deterministic toolkit for reproducible simulation.

This is the complete reference for the Threadlink runtime, its editor tooling and its authoring workflow, as of **Threadlink 6.6**.

## Organisation

Threadlink enforces a strict separation between authored data and implemented behaviour. The manual mirrors that separation across two self-contained volumes that share a common foundation.

| Part | Audience | Contents |
|---|---|---|
| [Getting Started](getting-started/requirements.md) | All | Requirements, installation and first-time project setup. |
| [Foundations](foundations/purpose.md) | All | Framework purpose, project layout, assembly topology, deployment overview. |
| [Designer Reference](designer/README.md) | Content authors | Identifier declaration, Vault authoring, spatial audio, input prompts, configuration tuning, content registration. Requires no C#. |
| [Engineer Reference](engineer/README.md) | Programmers | Architecture, deployment pipeline, subsystem APIs, PlayerLoop integration, Sentinel platform services, deterministic and memory tooling, code generation internals, custom subsystems, and performance. |
| [Reference](reference/iris-events.md) | All | Native Iris events, service access, identifier domains, menus and asset-creation entries, terminology. |

Both volumes describe the same systems from opposing ends. Where the designer volume specifies *"declare the identifier in `Vault.Fields.User.txt`"*, the engineer volume specifies the domain pipeline that emits `ThreadlinkIDs.Vault.Fields`, the manifest that stabilises its value, and the runtime API that consumes it.

> All API names, identifiers, menu paths, file paths and asset-creation entries in this manual are drawn from framework source. Systems that are stubbed or under development are identified as such.

## Where to start

- **New to Threadlink:** [Requirements](getting-started/requirements.md), then [Installation and Project Setup](getting-started/installation.md), then [Purpose](foundations/purpose.md).
- **Designers:** [The Identifier Workflow](designer/identifiers.md) and [The Vault](designer/vault.md).
- **Engineers:** [Architecture](engineer/architecture.md), [Deployment and Initialisation](engineer/deployment.md) and [Iris](engineer/iris.md).

---

*Threadlink Framework — Reference Manual. Developed and maintained by Threadforge.*

*Lead Developer: George Rontoulis*
