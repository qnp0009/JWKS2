using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

// Author: Quan Pham - student id: 11799707

public class KeyManager
{
    private readonly string _connectionString;

    // Constructor to initialize the KeyManager with a connection string
    public KeyManager(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase(); // Ensure the database is initialized when the KeyManager is created
    }

    // Initialize the database and create the 'keys' table if it doesn't exist
    private void InitializeDatabase()
    {
        try
        {
            Console.WriteLine("Initializing database...");

            // Ensure the database file exists
            if (!File.Exists("totally_not_my_privateKeys.db"))
            {
                Console.WriteLine("Database file does not exist. Creating a new one...");
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                Console.WriteLine("Database connection opened.");

                // Create the 'keys' table if it doesn't exist
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS keys (
                            kid INTEGER PRIMARY KEY AUTOINCREMENT,
                            key BLOB NOT NULL,
                            exp INTEGER NOT NULL
                        )";
                    command.ExecuteNonQuery();
                    Console.WriteLine("Database table created or verified.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing database: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    // Class to represent a key with its properties
    public class Key
    {
        public string Kid { get; set; } // Key ID
        public RSA RsaKey { get; set; } // RSA key pair
        public DateTime Expiry { get; set; } // Expiry timestamp
    }

    // Generate a new RSA key pair and store it in the database
    public Key GenerateKey(DateTime expiry)
    {
        try
        {
            Console.WriteLine($"Generating key with expiry: {expiry}");
            var rsa = RSA.Create(2048); // Generate a 2048-bit RSA key pair

            // Serialize the RSA key to PEM format
            var pem = ExportPrivateKey(rsa);

            // Store the key in the database
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                Console.WriteLine("Database connection opened for key storage.");

                using (var command = connection.CreateCommand())
                {
                    // SQL insertion with named parameters
                    command.CommandText = @"INSERT INTO keys (key, exp) VALUES (@key, @exp)";
                    command.Parameters.AddWithValue("@key", pem); // Named parameter
                    command.Parameters.AddWithValue("@exp", ((DateTimeOffset)expiry).ToUnixTimeSeconds()); // Named parameter
                    command.ExecuteNonQuery();
                    Console.WriteLine("Key stored in database.");
                }
            }

            // Retrieve the last inserted 'kid' from the database
            var kid = GetLastInsertedKid();
            return new Key { Kid = kid, RsaKey = rsa, Expiry = expiry };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating key: {ex.Message}");
            throw;
        }
    }

    // Helper method to get the last inserted 'kid' from the database
    private string GetLastInsertedKid()
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT last_insert_rowid()";
                var kid = command.ExecuteScalar().ToString(); // Get the last inserted 'kid' as a string
                return kid;
            }
        }
    }

    // Get all valid (non-expired) keys from the database
    public IEnumerable<Key> GetValidKeys()
    {
        var now = DateTime.UtcNow;
        var keys = new List<Key>();

        try
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT kid, key, exp FROM keys
                        WHERE exp > @now";
                    command.Parameters.AddWithValue("@now", ((DateTimeOffset)now).ToUnixTimeSeconds()); // Named parameter

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var kid = reader.GetInt32(0).ToString(); // Read 'kid' as an integer and convert to string
                            var pem = reader.GetString(1); // Read the PEM-encoded key
                            var exp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)).UtcDateTime; // Read the expiry timestamp

                            // Deserialize the RSA key from PEM format
                            var rsa = ImportPrivateKey(pem);

                            keys.Add(new Key { Kid = kid, RsaKey = rsa, Expiry = exp });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving valid keys: {ex.Message}");
        }

        return keys;
    }

    // Get the first expired key from the database
    public Key GetExpiredKey()
    {
        var now = DateTime.UtcNow;

        try
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT kid, key, exp FROM keys
                        WHERE exp <= @now
                        LIMIT 1";
                    command.Parameters.AddWithValue("@now", ((DateTimeOffset)now).ToUnixTimeSeconds()); // Named parameter

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var kid = reader.GetInt32(0).ToString(); // Read 'kid' as an integer and convert to string
                            var pem = reader.GetString(1); // Read the PEM-encoded key
                            var exp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)).UtcDateTime; // Read the expiry timestamp

                            // Deserialize the RSA key from PEM format
                            var rsa = ImportPrivateKey(pem);

                            return new Key { Kid = kid, RsaKey = rsa, Expiry = exp };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving expired key: {ex.Message}");
        }

        return null;
    }

    // Helper method to export RSA private key to PEM format
    private string ExportPrivateKey(RSA rsa)
    {
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        return Convert.ToBase64String(privateKeyBytes);
    }

    // Helper method to import RSA private key from PEM format
    private RSA ImportPrivateKey(string pem)
    {
        var privateKeyBytes = Convert.FromBase64String(pem);
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
        return rsa;
    }
}