---
title: Introduction
slug: /
sidebar_position: 1
---

SharpOMatic is an open-source workflow builder focused on AI-driven tasks.

## Configuration over code

One of the biggest challenges when building an AI-focused workflow is cycle time.
Constantly changing code to try new ideas slows you down.
AI is notorious for needing lots of experiments to find the right combination of model, prompts, tool calls, and glue logic to get the outcome you need.
Using SharpOMatic, you can prefer configuration over code, which lets you iterate on ideas much faster.

## Host your own execution

The execution engine is hosted in your project so you retain complete control over the environment and storage of all data.
You can run with OpenAI, Azure OpenAI, and Google connectors while still keeping all workflow and run data in your own environment.

## Deep integration

SharpOMatic is a .NET native project, so you can use familiar C# for glue logic in workflow code nodes.
You can call from code nodes directly into your backend code for fast, simple integration.
Expose your C# types for structured outputs and C# functions for tool calling.

## What you can do

- Build workflows with nodes for models, code, branching, and orchestration.
- Run and debug workflows with rich run state and persisted history.
- Configure and run evaluations against datasets with grader workflows.
- Embed the editor into your own ASP.NET Core host.
- Call into your existing backend code from the workflow.

## Project layout

- Frontend UI: `src/SharpOMatic.FrontEnd`
- Editor host: `src/SharpOMatic.Editor`
- Workflow engine: `src/SharpOMatic.Engine`
- Sqlite for storage: `src/SharpOMatic.Engine.Sqlite`
- SQL Server for storage: `src/SharpOMatic.Engine.SqlServer`
- Sample host: `src/SharpOMatic.DemoServer`

## What next?

Use the [Getting Started](./getting-started/start-with-github-code.md) guides to get up and running in just a few minutes.

Read the [Core Concepts](./core-concepts/workflows.md) section before continuing to more specific features.

To evaluate your workflows, go to [Evaluations](./core-concepts/evaluations.md).


