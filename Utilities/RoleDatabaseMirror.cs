using Eventify.Models;
using Microsoft.Data.Sqlite;

namespace Eventify.Utilities;

public static class RoleDatabaseMirror
{
    public static async Task MirrorUserAsync(IConfiguration config, UserAccount user)
    {
        var connectionString = user.Role switch
        {
            "admin" => config.GetConnectionString("AdminConnection"),
            "organizer" => config.GetConnectionString("OrganizerConnection"),
            "attend" => config.GetConnectionString("AttendConnection"),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        await using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    PasswordText TEXT NOT NULL DEFAULT '',
                    Role TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                """;
            await createCmd.ExecuteNonQueryAsync();
        }

        await using (var alterCmd = conn.CreateCommand())
        {
            alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN PasswordText TEXT NOT NULL DEFAULT '';";
            try
            {
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch
            {
            }
        }

        await using (var indexCmd = conn.CreateCommand())
        {
            indexCmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email);";
            await indexCmd.ExecuteNonQueryAsync();
        }

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText =
            """
            INSERT INTO Users (FullName, Email, PasswordHash, PasswordText, Role, CreatedAtUtc)
            VALUES ($fullName, $email, $passwordHash, $passwordText, $role, $createdAtUtc)
            ON CONFLICT(Email) DO UPDATE SET
                FullName = excluded.FullName,
                PasswordHash = excluded.PasswordHash,
                PasswordText = excluded.PasswordText,
                Role = excluded.Role,
                CreatedAtUtc = excluded.CreatedAtUtc;
            """;
        insertCmd.Parameters.AddWithValue("$fullName", user.FullName);
        insertCmd.Parameters.AddWithValue("$email", user.Email);
        insertCmd.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        insertCmd.Parameters.AddWithValue("$passwordText", user.PasswordText ?? string.Empty);
        insertCmd.Parameters.AddWithValue("$role", user.Role);
        insertCmd.Parameters.AddWithValue("$createdAtUtc", user.CreatedAtUtc.ToString("O"));
        await insertCmd.ExecuteNonQueryAsync();
    }

    public static async Task SyncAllUsersAsync(IConfiguration config, IEnumerable<UserAccount> users)
    {
        foreach (var user in users)
        {
            await MirrorUserAsync(config, user);
        }
    }
    public static async Task RemoveUserAsync(IConfiguration config, string email)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var connections = new[]
        {
            config.GetConnectionString("AdminConnection"),
            config.GetConnectionString("OrganizerConnection"),
            config.GetConnectionString("AttendConnection")
        };

        foreach (var connectionString in connections.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Users WHERE lower(Email) = $email;";
            cmd.Parameters.AddWithValue("$email", normalizedEmail);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

