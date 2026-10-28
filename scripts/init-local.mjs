import { randomBytes } from "node:crypto";
import { writeFileSync } from "node:fs";

try {
    writeFileSync(".env", [
        "POSTGRES_DB=devsync",
        "POSTGRES_USER=devsync",
        `POSTGRES_PASSWORD=${randomBytes(24).toString("hex")}`,
        "POSTGRES_PORT=54329",
        "WEB_PORT=5180",
        ""
    ].join("\n"), { flag: "wx", mode: 0o600 });
    console.log("Created .env with a random local database password.");
} catch (error) {
    if (error.code !== "EEXIST") throw error;
    console.log(".env already exists; preserved the current local configuration.");
}
