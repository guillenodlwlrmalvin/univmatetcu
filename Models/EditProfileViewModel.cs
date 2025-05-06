using System.ComponentModel.DataAnnotations;

namespace UnivMate.Models
{
    public class EditProfileViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3-50 characters")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; }

        [DataType(DataType.Upload)]
        [FileExtensions(Extensions = "jpg,jpeg,png", ErrorMessage = "Only JPG, JPEG or PNG files allowed")]
        public IFormFile ProfilePicture { get; set; }

        public string ExistingProfilePicturePath { get; set; }

        // Role-specific properties
        public string Role { get; set; }

        [Display(Name = "Student ID")]
        public string StudentId { get; set; }

        public string Major { get; set; }

        public string Department { get; set; }

        public string Position { get; set; }

        [Display(Name = "Staff ID")]
        public string StaffId { get; set; }

        [Display(Name = "Office Location")]
        public string OfficeLocation { get; set; }
    }
}   