---
title: Switch Node
sidebar_position: 102
---

The switch node routes execution to a single output based on the evaluation of ordered C# expressions.
Each switch entry has a name and a boolean expression that determines whether its branch is taken.

## Expressions

Expressions are evaluated in order.
The first entry that returns **true** is selected and all remaining entries are skipped.
Expressions have access to the workflow **Context** object during evaluation.

Each expression must return a **bool** value.
If an expression returns **null** or a non-boolean type, the node fails with an error.
You can use multiple lines of code to handle complex calculations.
Ensure you return a value from all possible code paths.

<img src="/img/switch_example.png" alt="Switch expressions" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

## Default Branch

The last entry is the default branch.
It has no expression, cannot be removed, and always runs if no earlier expression matches.
This guarantees that the node always selects a path, even when all conditions are false.
Each switch branch has a name so you can distinguish between them in the editor.

<img src="/img/switch_editor.png" alt="Switch editor" width="500" style={{ maxWidth: '100%', height: 'auto' }} />
