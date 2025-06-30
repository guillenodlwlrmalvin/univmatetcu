using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace UnivMate.Models
{
    public class RegisterViewModel : IValidatableObject


    {


        [Required]
        public string Username { get; set; } 
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
    ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        // Role Selection
        [Required(ErrorMessage = "Please select your role")]
        [Display(Name = "Role")]
        public string Role { get; set; }

        // STUDENT FIELDS
        [Required(ErrorMessage = "Student ID is required")]
        [RegularExpression(@"^\d{2}-\d{5}$", ErrorMessage = "Student ID must be in format 00-00000")]
        [Display(Name = "Student ID")]
        public string StudentId { get; set; }

        [Display(Name = "Major")]
        public string Major { get; set; }

        // PROFESSOR FIELDS
        [Display(Name = "Department")]
        public string Department { get; set; }

        [Display(Name = "Position")]
        public string Position { get; set; }

        // STAFF FIELDS
        [Display(Name = "Staff ID")]
        public string StaffId { get; set; }

        [Display(Name = "Office Location")]
        public string OfficeLocation { get; set; }

        // Conditional validation logic
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Role == "Student")
            {
                if (string.IsNullOrWhiteSpace(StudentId))
                    yield return new ValidationResult("Student ID is required.", new[] { nameof(StudentId) });

                if (string.IsNullOrWhiteSpace(Major))
                    yield return new ValidationResult("Major is required.", new[] { nameof(Major) });
            }
            else if (Role == "Professor")
            {
                if (string.IsNullOrWhiteSpace(Department))
                    yield return new ValidationResult("Department is required.", new[] { nameof(Department) });

                if (string.IsNullOrWhiteSpace(Position))
                    yield return new ValidationResult("Position is required.", new[] { nameof(Position) });
            }
            else if (Role == "Staff")
            {
                if (string.IsNullOrWhiteSpace(StaffId))
                    yield return new ValidationResult("Staff ID is required.", new[] { nameof(StaffId) });

                if (string.IsNullOrWhiteSpace(OfficeLocation))
                    yield return new ValidationResult("Office Location is required.", new[] { nameof(OfficeLocation) });
            }
        }
    }
}