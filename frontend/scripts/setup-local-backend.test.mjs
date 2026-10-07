import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
    ensureFrontendEnvironment,
    setupLocalBackend,
} from "./setup-local-backend.mjs";

test("creates the local frontend environment when it is missing", async () => {
    const directory = await mkdtemp(join(tmpdir(), "nordiska-frontend-env-"));

    try {
        const frontendDirectory = join(directory, "frontend");
        await mkdir(frontendDirectory);
        await writeFile(
            join(frontendDirectory, ".env.example"),
            "VITE_API_BASE_URL=http://localhost:5031/api\n",
        );

        const result = await ensureFrontendEnvironment(frontendDirectory);

        assert.equal(result, "created");
        assert.equal(
            await readFile(join(frontendDirectory, ".env.local"), "utf8"),
            "VITE_API_BASE_URL=http://localhost:5031/api\n",
        );
    } finally {
        await rm(directory, { recursive: true, force: true });
    }
});

test("preserves an existing local frontend environment", async () => {
    const directory = await mkdtemp(join(tmpdir(), "nordiska-frontend-env-"));

    try {
        const frontendDirectory = join(directory, "frontend");
        await mkdir(frontendDirectory);
        await writeFile(
            join(frontendDirectory, ".env.example"),
            "VITE_API_BASE_URL=http://localhost:5031/api\n",
        );
        await writeFile(
            join(frontendDirectory, ".env.local"),
            "VITE_API_BASE_URL=http://custom-backend.test/api\n",
        );

        const result = await ensureFrontendEnvironment(frontendDirectory);

        assert.equal(result, "existing");
        assert.equal(
            await readFile(join(frontendDirectory, ".env.local"), "utf8"),
            "VITE_API_BASE_URL=http://custom-backend.test/api\n",
        );
    } finally {
        await rm(directory, { recursive: true, force: true });
    }
});

test("starts only the database, API and reporting worker Docker services", async () => {
    const directory = await mkdtemp(join(tmpdir(), "nordiska-local-backend-"));

    try {
        const frontendDirectory = join(directory, "frontend");
        const infrastructureDirectory = join(directory, "infra", "v2");
        await mkdir(frontendDirectory, { recursive: true });
        await mkdir(infrastructureDirectory, { recursive: true });
        await writeFile(
            join(frontendDirectory, ".env.example"),
            "VITE_API_BASE_URL=http://localhost:5031/api\n",
        );
        await writeFile(join(infrastructureDirectory, ".env"), "LOCAL_ONLY=1\n");

        const executions = [];
        const execute = (command, arguments_, options) => {
            executions.push({ command, arguments_, options });
            return { status: 0 };
        };

        await setupLocalBackend({ repoRoot: directory, execute });

        assert.deepEqual(executions, [
            {
                command: "docker",
                arguments_: [
                    "compose",
                    "--project-name",
                    "nordiska-v2",
                    "--env-file",
                    ".env",
                    "-f",
                    "docker-compose.yml",
                    "-f",
                    "docker-compose.override.yml",
                    "up",
                    "-d",
                    "--build",
                    "db",
                    "api",
                    "reporting-worker",
                ],
                options: {
                    cwd: infrastructureDirectory,
                    stdio: "inherit",
                },
            },
        ]);
    } finally {
        await rm(directory, { recursive: true, force: true });
    }
});

test("requires DevSetup before starting Docker when the backend environment is missing", async () => {
    const directory = await mkdtemp(join(tmpdir(), "nordiska-local-backend-"));

    try {
        const frontendDirectory = join(directory, "frontend");
        await mkdir(frontendDirectory, { recursive: true });
        await mkdir(join(directory, "infra", "v2"), { recursive: true });
        await writeFile(
            join(frontendDirectory, ".env.example"),
            "VITE_API_BASE_URL=http://localhost:5031/api\n",
        );

        let executionCount = 0;
        const execute = () => {
            executionCount += 1;
            return { status: 0 };
        };

        await assert.rejects(
            setupLocalBackend({ repoRoot: directory, execute }),
            /DevSetup/,
        );
        assert.equal(executionCount, 0);
    } finally {
        await rm(directory, { recursive: true, force: true });
    }
});
