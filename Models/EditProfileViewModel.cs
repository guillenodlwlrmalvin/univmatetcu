using System.ComponentModel.DataAnnotations;

namespace UnivMate.Models
{
    public class EditProfileViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Username cannot be longer than 50 characters.")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "First name cannot be longer than 50 characters.")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Last name cannot be longer than 50 characters.")]
        public string LastName { get; set; }

        public string ExistingProfilePicturePath { get; set; }

        public string Role { get; set; }

        [StringLength(20, ErrorMessage = "Student ID cannot be longer than 20 characters.")]
        public string StudentId { get; set; }

        [StringLength(50, ErrorMessage = "Major cannot be longer than 50 characters.")]
        public string Major { get; set; }

        [StringLength(50, ErrorMessage = "Department cannot be longer than 50 characters.")]
        public string Department { get; set; }

        [StringLength(50, ErrorMessage = "Position cannot be longer than 50 characters.")]
        public string Position { get; set; }

        [StringLength(20, ErrorMessage = "Staff ID cannot be longer than 20 characters.")]
        public string StaffId { get; set; }

        [StringLength(100, ErrorMessage = "Office location cannot be longer than 100 characters.")]
        public string OfficeLocation { get; set; }
    }

}