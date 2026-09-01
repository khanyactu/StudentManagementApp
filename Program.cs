using System;
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace StudentManagementApp
{
    internal class Program
    {
        private static readonly string ConnectionString = LoadConnectionString();

        private static string LoadConnectionString()
        {
            string fromEnv = Environment.GetEnvironmentVariable("SMIS_CONNECTION");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;

            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    return doc.RootElement
                              .GetProperty("ConnectionStrings")
                              .GetProperty("StudentManagementDB")
                              .GetString();
                }
            }

            throw new InvalidOperationException(
                "No connection string found. Set the SMIS_CONNECTION environment variable " +
                "or provide appsettings.json.");
        }

        private static void Main()
        {
            bool running = true;
            while (running)
            {
                Console.WriteLine();
                Console.WriteLine("STUDENT MANAGEMENT SYSTEM");
                Console.WriteLine();
                Console.WriteLine("1. Display all students");
                Console.WriteLine("2. Search for a student");
                Console.WriteLine("3. Register a student");
                Console.WriteLine("4. Enrol a student");
                Console.WriteLine("5. Capture or update a mark");
                Console.WriteLine("6. View student results");
                Console.WriteLine("7. View students without enrolments");
                Console.WriteLine("8. Record a payment");
                Console.WriteLine("9. Exit");
                Console.Write("\nChoose an option (1-9): ");

                string choice = Console.ReadLine();
                Console.WriteLine();

                try
                {
                    switch (choice)
                    {
                        case "1": DisplayAllStudents(); break;
                        case "2": SearchStudent(); break;
                        case "3": RegisterStudent(); break;
                        case "4": EnrolStudent(); break;
                        case "5": CaptureMark(); break;
                        case "6": ViewStudentResults(); break;
                        case "7": ViewStudentsWithoutEnrolments(); break;
                        case "8": RecordPayment(); break;
                        case "9": running = false; break;
                        default:
                            Console.WriteLine("Invalid option. Please enter a number from 1 to 9.");
                            break;
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine("Database error: " + FriendlyMessage(ex));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }

            Console.WriteLine("Goodbye.");
        }

        // MENU OPTION 1 - Display all students

        private static void DisplayAllStudents()
        {
            const string sql = @"SELECT StudentID, StudentNumber, FullName, Email, Status
                                 FROM dbo.STUDENT
                                 ORDER BY StudentNumber;";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("No students are registered yet.");
                        return;
                    }

                    Console.WriteLine("{0,-5} {1,-10} {2,-25} {3,-32} {4}",
                                      "ID", "Number", "Full name", "Email", "Status");
                    Console.WriteLine(new string('-', 90));

                    while (reader.Read())
                    {
                        Console.WriteLine("{0,-5} {1,-10} {2,-25} {3,-32} {4}",
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4));
                    }
                }
            }
        }

        // MENU OPTION 2 - Search for a student by student number

        private static void SearchStudent()
        {
            string number = ReadRequired("Enter the student number: ");

            const string sql = @"SELECT StudentID, StudentNumber, FullName, Email, Status
                                 FROM dbo.STUDENT
                                 WHERE StudentNumber = @StudentNumber;";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@StudentNumber", SqlDbType.NVarChar, 20).Value = number;

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        Console.WriteLine("No student found with student number " + number + ".");
                        return;
                    }

                    Console.WriteLine("Student ID     : " + reader.GetInt32(0));
                    Console.WriteLine("Student number : " + reader.GetString(1));
                    Console.WriteLine("Full name      : " + reader.GetString(2));
                    Console.WriteLine("Email          : " + reader.GetString(3));
                    Console.WriteLine("Status         : " + reader.GetString(4));
                }
            }
        }

        // MENU OPTION 3 - Register a student
        private static void RegisterStudent()
        {
            string number = ReadRequired("Student number : ");
            string name = ReadRequired("Full name      : ");
            string email = ReadRequired("Email          : ");

            const string sql = @"INSERT INTO dbo.STUDENT (StudentNumber, FullName, Email, Status)
                                 VALUES (@StudentNumber, @FullName, @Email, 'Inactive');
                                 SELECT SCOPE_IDENTITY();";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@StudentNumber", SqlDbType.NVarChar, 20).Value = number;
                cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = name;
                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = email;

                conn.Open();
                object newId = cmd.ExecuteScalar();
                Console.WriteLine("Student registered successfully. New StudentID = " +
                                  Convert.ToInt32(newId));
            }
        }

        // MENU OPTION 4 - Enrol a student (calls the safe transaction)

        private static void EnrolStudent()
        {
            int studentId = ReadInt("Student ID : ");
            int courseId = ReadInt("Course ID  : ");

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand("dbo.usp_EnrolStudent", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@StudentID", SqlDbType.Int).Value = studentId;
                cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = courseId;

                conn.Open();
                cmd.ExecuteNonQuery();
                Console.WriteLine("Enrolment created and the student status is now Active.");
            }
        }

        // MENU OPTION 5 - Capture or update a mark (fires the audit trigger)

        private static void CaptureMark()
        {
            int studentId = ReadInt("Student ID : ");
            int courseId = ReadInt("Course ID  : ");
            decimal mark = ReadMark("Final mark (0-100) : ");

            const string sql = @"UPDATE dbo.ENROLMENT
                                 SET FinalMark = @FinalMark
                                 WHERE StudentID = @StudentID AND CourseID = @CourseID;";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@FinalMark", SqlDbType.Decimal).Value = mark;
                cmd.Parameters["@FinalMark"].Precision = 5;
                cmd.Parameters["@FinalMark"].Scale = 2;
                cmd.Parameters.Add("@StudentID", SqlDbType.Int).Value = studentId;
                cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = courseId;

                conn.Open();
                int rows = cmd.ExecuteNonQuery();

                Console.WriteLine(rows == 0
                    ? "That student is not enrolled in that course, so no mark was saved."
                    : "Mark saved. The change was written to the audit table.");
            }
        }

        // MENU OPTION 6 - View student results (calls the stored procedure)

        private static void ViewStudentResults()
        {
            int studentId = ReadInt("Student ID : ");

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand("dbo.usp_GetStudentResults", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@StudentID", SqlDbType.Int).Value = studentId;

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("No results found for that student.");
                        return;
                    }

                    Console.WriteLine("{0,-10} {1,-25} {2,-10} {3,-25} {4,-8} {5}",
                                      "Number", "Full name", "Code", "Course", "Mark", "Result");
                    Console.WriteLine(new string('-', 92));

                    while (reader.Read())
                    {
                        // Column 4 (FinalMark) may be NULL - handle it safely.
                        string mark = reader.IsDBNull(4)
                            ? "-"
                            : reader.GetDecimal(4).ToString("0.00");

                        Console.WriteLine("{0,-10} {1,-25} {2,-10} {3,-25} {4,-8} {5}",
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            mark,
                            reader.GetString(5));
                    }
                }
            }
        }

        // MENU OPTION 7 - Students without enrolments (LEFT JOIN)

        private static void ViewStudentsWithoutEnrolments()
        {
            const string sql = @"SELECT s.StudentNumber, s.FullName
                                 FROM dbo.STUDENT AS s
                                 LEFT JOIN dbo.ENROLMENT AS e ON e.StudentID = s.StudentID
                                 WHERE e.StudentID IS NULL
                                 ORDER BY s.StudentNumber;";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("Every student is enrolled in at least one course.");
                        return;
                    }

                    while (reader.Read())
                        Console.WriteLine(reader.GetString(0) + "  " + reader.GetString(1));
                }
            }
        }

        // MENU OPTION 8 - Record a payment

        private static void RecordPayment()
        {
            int studentId = ReadInt("Student ID       : ");
            decimal amount = ReadPositiveDecimal("Amount           : ");
            DateTime date = ReadDate("Payment date (yyyy-mm-dd) : ");
            string reference = ReadRequired("Reference number : ");

            const string sql = @"INSERT INTO dbo.PAYMENT (StudentID, Amount, PaymentDate, ReferenceNumber)
                                 VALUES (@StudentID, @Amount, @PaymentDate, @ReferenceNumber);";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@StudentID", SqlDbType.Int).Value = studentId;
                cmd.Parameters.Add("@Amount", SqlDbType.Decimal).Value = amount;
                cmd.Parameters["@Amount"].Precision = 10;
                cmd.Parameters["@Amount"].Scale = 2;
                cmd.Parameters.Add("@PaymentDate", SqlDbType.Date).Value = date;
                cmd.Parameters.Add("@ReferenceNumber", SqlDbType.NVarChar, 30).Value = reference;

                conn.Open();
                cmd.ExecuteNonQuery();
                Console.WriteLine("Payment recorded successfully.");
            }
        }

        // INPUT VALIDATION HELPERS

        private static string ReadRequired(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string value = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
                Console.WriteLine("This field is required.");
            }
        }

        private static int ReadInt(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int value) && value > 0)
                    return value;
                Console.WriteLine("Please enter a whole number greater than zero.");
            }
        }

        private static decimal ReadMark(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (decimal.TryParse(Console.ReadLine(), out decimal value)
                    && value >= 0 && value <= 100)
                    return value;
                Console.WriteLine("A mark must be between 0 and 100.");
            }
        }

        private static decimal ReadPositiveDecimal(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (decimal.TryParse(Console.ReadLine(), out decimal value) && value > 0)
                    return value;
                Console.WriteLine("The amount must be greater than zero.");
            }
        }

        private static DateTime ReadDate(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (DateTime.TryParse(Console.ReadLine(), out DateTime value))
                    return value;
                Console.WriteLine("Please enter a valid date, for example 2026-03-15.");
            }
        }

        // Turns raw SQL Server errors into something a user can act on.
        private static string FriendlyMessage(SqlException ex)
        {
            switch (ex.Number)
            {
                case 2627:   // unique constraint / PK violation
                case 2601:
                    return "That record already exists. Student numbers, emails and " +
                           "reference numbers must be unique, and a student cannot be " +
                           "enrolled in the same course twice.";
                case 547:    // CHECK or FOREIGN KEY violation
                    return "The value is not allowed. Check that the student and course " +
                           "exist, that a mark is between 0 and 100, and that a payment " +
                           "amount is greater than zero.";
                case 50001:
                    return ex.Message;
                case 53:
                case 18456:
                    return "Cannot connect to SQL Server. Check the server name and your " +
                           "login details in the connection string.";
                default:
                    return ex.Message;
            }
        }
    }
}