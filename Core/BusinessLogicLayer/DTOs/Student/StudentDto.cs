namespace BusinessLayer.DTOs.Student
{
    public class StudentDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhotoUrl { get; set; }
    }
}