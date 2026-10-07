import { spawnSync } from "node:child_process";
import { access, constants, copyFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export async function ensureFrontendEnvironment(frontendDirectory) {
    try {
        await copyFile(
            join(frontendDirectory, ".env.example"),
            join(frontendDirectory, ".env.local"),
            constants.COPYFILE_EXCL,
        );
    } catch (error) {
        if (error?.code === "EEXIST") {
            return "existing";
        }

        throw error;
    }

    return "created";
}

export async function setupLocalBackend({
    repoRoot,
    execute = spawnSync,
}) {
    const frontendDirectory = join(repoRoot, "frontend");
    const infrastructureDirectory = join(repoRoot, "infra", "v2");

    const environmentStatus = await ensureFrontendEnvironment(frontendDirectory);

    try {
        await access(join(infrastructureDirectory, ".env"));
    } catch (error) {
        if (error?.code === "ENOENT") {
            throw new Error(
                "infra/v2/.env is missing. Run Nordiska.DevSetup once before starting the local backend.",
            );
        }

        throw error;
    }

    const result = execute(
        "docker",
        [
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
        {
            cwd: infrastructureDirectory,
            stdio: "inherit",
        },
    );

    if (result.error) {
        throw result.error;
    }

    if (result.status !== 0) {
        throw new Error(`Docker Compose exited with code ${result.status}.`);
    }

    return { environmentStatus };
}

const currentFile = fileURLToPath(import.meta.url);

if (process.argv[1] && resolve(process.argv[1]) === currentFile) {
    const repoRoot = resolve(dirname(currentFile), "..", "..");

    try {
        console.log("[SETUP] Preparing the local frontend environment...");
        const { environmentStatus } = await setupLocalBackend({ repoRoot });

        if (environmentStatus === "created") {
            console.log("[OK] Created frontend/.env.local from .env.example.");
        } else {
            console.log("[OK] Kept the existing frontend/.env.local.");
        }

        console.log("[SUCCESS] Database, API and Reporting Worker are running in Docker.");
        console.log("[NEXT] Start the frontend with: npm run dev");
    } catch (error) {
        console.error(`[ERROR] ${error.message}`);
        process.exitCode = 1;
    }
}
