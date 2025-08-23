using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipboardHistoryManager.Data
{
    public static class Database
    {
        private static string _dbPath = "clipboard.db";
        private static string _connStr = $"Data Source={_dbPath};Version=3;";

        public static void Init()
        {
            if (!File.Exists(_dbPath))
                SQLiteConnection.CreateFile(_dbPath);

            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = @"CREATE TABLE IF NOT EXISTS ClipboardHistory (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Timestamp TEXT,
                            Type TEXT,
                            Content TEXT,
                            Tag TEXT
                          )";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        public static void Insert(ClipboardItem item)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "INSERT INTO ClipboardHistory (Timestamp, Type, Content, Tag) VALUES (@Timestamp, @Type, @Content, @Tag)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Timestamp", item.Timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("@Type", item.Type);
            cmd.Parameters.AddWithValue("@Content", item.Content);
            cmd.Parameters.AddWithValue("@Tag", item.Tag);
            cmd.ExecuteNonQuery();
        }

        public static void Delete(int id)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "DELETE FROM ClipboardHistory WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public static string GetContent(int id)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "SELECT Content FROM ClipboardHistory WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            object result = cmd.ExecuteScalar();

            return result?.ToString();
        }

        public static void UpdateTimestamp(int id)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "UPDATE ClipboardHistory SET Timestamp = @Timestamp WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateTag(int id, string tag)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "UPDATE ClipboardHistory SET Tag = @Tag WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Tag", tag);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public static List<string> GetAllAvailableTags()
        {
            var list = new List<string>();
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "SELECT DISTINCT Tag FROM ClipboardHistory WHERE Tag IS NOT NULL AND Tag <> ''";
            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["Tag"].ToString());
            }

            // Add a default "Favorite" tag if not already present
            if (!list.Contains("Favorite"))
            {
                list.Add("Favorite");
            }
            
            return list;
        }

        public static List<ClipboardItem> GetByTag(string tag)
        {
            var list = new List<ClipboardItem>();
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "SELECT * FROM ClipboardHistory WHERE Tag = @Tag ORDER BY Timestamp DESC";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Tag", tag);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ClipboardItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Timestamp = DateTime.Parse(reader["Timestamp"].ToString()),
                    Type = reader["Type"].ToString(),
                    Content = reader["Content"].ToString(),
                    Tag = reader["Tag"].ToString()
                });
            }
            return list;
        }

        public static List<ClipboardItem> GetAll()
        {
            var list = new List<ClipboardItem>();
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            string sql = "SELECT * FROM ClipboardHistory ORDER BY Timestamp DESC";
            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ClipboardItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Timestamp = DateTime.Parse(reader["Timestamp"].ToString()),
                    Type = reader["Type"].ToString(),
                    Content = reader["Content"].ToString(),
                    Tag = reader["Tag"].ToString()
                });
            }
            return list;
        }
    }
}
