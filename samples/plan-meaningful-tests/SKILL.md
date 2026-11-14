# Plan meaningful tests

## Purpose
Turn a feature or code change into a focused test plan that checks observable behavior and meaningful failure cases.

## Instructions
1. Read the requirement, relevant code, and existing tests. Ask about unclear expected behavior before treating assumptions as facts.
2. Identify the public inputs, outputs, side effects, and dependencies. Describe what a user or caller should observe.
3. Propose normal, boundary, invalid-input, and dependency-failure cases where they are relevant. Avoid tests that only repeat implementation details.
4. For each case, specify concrete setup, input, expected outcome, and the regression it would catch.
5. Choose the smallest useful test level: unit for isolated behavior, integration for component boundaries, and browser or end-to-end tests for critical user workflows.
6. Prioritize cases by impact and likelihood. Separate essential coverage from optional follow-up work.
7. Use templates/test-plan.md to present the plan. Distinguish proposed checks from checks actually executed; never invent results.

Do not execute commands or modify files unless the developer authorizes it.
