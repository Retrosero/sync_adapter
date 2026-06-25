using Npgsql;

var conn = "Host=127.0.0.1;Port=5432;Database=fieldops;Username=fieldops;Password=fieldops_dev_pwd;SSL Mode=Disable";
Console.WriteLine("Connection: " + conn);
try
{
    using var connection = new NpgsqlConnection(conn);
    connection.Open();
    Console.WriteLine("Connected!");
    using var cmd = new NpgsqlCommand("SELECT current_user, inet_server_addr(), inet_server_port()", connection);
    using var rd = cmd.ExecuteReader();
    rd.Read();
    Console.WriteLine("User: " + rd.GetString(0));
    Console.WriteLine("Server addr: " + rd.GetValue(1));
    Console.WriteLine("Server port: " + rd.GetValue(2));
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: " + ex.Message);
}
