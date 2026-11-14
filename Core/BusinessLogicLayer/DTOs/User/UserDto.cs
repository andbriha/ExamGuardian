using System;
using System.Collections.Generic;
using System.Text;

namespace BusinessLogicLayer.DTOs.User
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? Group { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
