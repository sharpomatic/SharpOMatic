---
title: "Basic: Call backend code"
---

This sample shows how **Code** nodes can call classes and methods from the host application.

## Choose this when

Use this sample when you want workflow code to reuse backend services, helper classes, or application logic instead of duplicating it inside the workflow.

## What it demonstrates

- Reading a numeric input from context.
- Instantiating a backend class from a **Code** node.
- Calling an async static backend method.
- Writing multiple output values under `output`.

## How it works

The workflow starts with an optional `number` input defaulting to `42`.
One Code node creates a `CodeExample` instance and writes `output.doubled`.
The next Code node awaits `CodeExample.SlowDoubled` and writes `output.slowDoubled` before the workflow ends.

## Setup notes

This sample depends on the 'DemoServer' host code that defines `CodeExample`.
In your own host application, use the same pattern with classes or services available to your workflow code.
