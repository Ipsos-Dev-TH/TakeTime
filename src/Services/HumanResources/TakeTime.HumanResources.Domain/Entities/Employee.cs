using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.HumanResources.Domain.Enums;

namespace TakeTime.HumanResources.Domain.Entities;

/// <summary>
/// Aggregate root representing an employee of the property.
/// Tracks personal information, employment details, and related documents.
/// </summary>
public class Employee : AggregateRoot
{
    private readonly List<string> _documents = [];
    private readonly List<string> _contracts = [];

    /// <summary>
    /// Unique employee code for identification.
    /// </summary>
    public string EmployeeCode { get; private set; } = string.Empty;

    /// <summary>
    /// Employee's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Employee's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Employee's nickname (common in Thai culture).
    /// </summary>
    public string? NickName { get; private set; }

    /// <summary>
    /// Date of birth.
    /// </summary>
    public DateTime DateOfBirth { get; private set; }

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public PhoneNumber Phone { get; private set; } = null!;

    /// <summary>
    /// Email address.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Home address.
    /// </summary>
    public string? Address { get; private set; }

    /// <summary>
    /// Job position/title.
    /// </summary>
    public string Position { get; private set; } = string.Empty;

    /// <summary>
    /// Department the employee belongs to.
    /// </summary>
    public string Department { get; private set; } = string.Empty;

    /// <summary>
    /// Employment start date.
    /// </summary>
    public DateTime StartDate { get; private set; }

    /// <summary>
    /// Employment end date (null if currently employed).
    /// </summary>
    public DateTime? EndDate { get; private set; }

    /// <summary>
    /// Monthly base salary.
    /// </summary>
    public Money Salary { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Bank account number for salary payment.
    /// </summary>
    public string? BankAccount { get; private set; }

    /// <summary>
    /// Type of employment arrangement.
    /// </summary>
    public EmploymentType EmploymentType { get; private set; }

    /// <summary>
    /// Identifier of the direct supervisor.
    /// </summary>
    public Guid? SupervisorId { get; private set; }

    /// <summary>
    /// Current employment status.
    /// </summary>
    public EmployeeStatus Status { get; private set; }

    /// <summary>
    /// Full name for display purposes.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Document URLs or references associated with the employee.
    /// </summary>
    public IReadOnlyCollection<string> Documents => _documents.AsReadOnly();

    /// <summary>
    /// Contract document URLs or references.
    /// </summary>
    public IReadOnlyCollection<string> Contracts => _contracts.AsReadOnly();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Employee() { }

    /// <summary>
    /// Creates a new employee.
    /// </summary>
    public Employee(
        string employeeCode,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        PhoneNumber phone,
        string position,
        string department,
        DateTime startDate,
        Money salary,
        EmploymentType employmentType,
        string? nickName = null,
        string? email = null,
        string? address = null,
        string? bankAccount = null,
        Guid? supervisorId = null)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new ArgumentException("Employee code is required.", nameof(employeeCode));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (string.IsNullOrWhiteSpace(position))
            throw new ArgumentException("Position is required.", nameof(position));

        if (string.IsNullOrWhiteSpace(department))
            throw new ArgumentException("Department is required.", nameof(department));

        ArgumentNullException.ThrowIfNull(phone);

        EmployeeCode = employeeCode;
        FirstName = firstName;
        LastName = lastName;
        NickName = nickName;
        DateOfBirth = dateOfBirth.Date;
        Phone = phone;
        Email = email;
        Address = address;
        Position = position;
        Department = department;
        StartDate = startDate.Date;
        Salary = salary;
        BankAccount = bankAccount;
        EmploymentType = employmentType;
        SupervisorId = supervisorId;
        Status = EmployeeStatus.Active;
    }

    /// <summary>
    /// Updates the employee's personal information.
    /// </summary>
    public void UpdatePersonalInfo(
        string firstName,
        string lastName,
        string? nickName,
        string? email,
        string? address,
        PhoneNumber phone)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        FirstName = firstName;
        LastName = lastName;
        NickName = nickName;
        Email = email;
        Address = address;
        Phone = phone;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the employee's employment details.
    /// </summary>
    public void UpdateEmployment(
        string position,
        string department,
        Money salary,
        EmploymentType employmentType,
        Guid? supervisorId = null)
    {
        Position = position;
        Department = department;
        Salary = salary;
        EmploymentType = employmentType;
        SupervisorId = supervisorId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records the employee's resignation.
    /// </summary>
    public void Resign(DateTime endDate)
    {
        if (Status != EmployeeStatus.Active)
            throw new InvalidOperationException(
                $"Cannot resign employee in '{Status}' status.");

        Status = EmployeeStatus.Resigned;
        EndDate = endDate.Date;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Terminates the employee.
    /// </summary>
    public void Terminate(DateTime endDate)
    {
        if (Status != EmployeeStatus.Active)
            throw new InvalidOperationException(
                $"Cannot terminate employee in '{Status}' status.");

        Status = EmployeeStatus.Terminated;
        EndDate = endDate.Date;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the employee temporarily.
    /// </summary>
    public void Deactivate()
    {
        Status = EmployeeStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates the employee.
    /// </summary>
    public void Reactivate()
    {
        if (Status != EmployeeStatus.Inactive)
            throw new InvalidOperationException(
                $"Cannot reactivate employee in '{Status}' status. Only Inactive employees can be reactivated.");

        Status = EmployeeStatus.Active;
        EndDate = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a document reference to the employee.
    /// </summary>
    public void AddDocument(string documentUrl)
    {
        if (string.IsNullOrWhiteSpace(documentUrl))
            throw new ArgumentException("Document URL is required.", nameof(documentUrl));

        _documents.Add(documentUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a contract reference to the employee.
    /// </summary>
    public void AddContract(string contractUrl)
    {
        if (string.IsNullOrWhiteSpace(contractUrl))
            throw new ArgumentException("Contract URL is required.", nameof(contractUrl));

        _contracts.Add(contractUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the bank account for salary payment.
    /// </summary>
    public void SetBankAccount(string bankAccount)
    {
        BankAccount = bankAccount;
        UpdatedAt = DateTime.UtcNow;
    }
}
