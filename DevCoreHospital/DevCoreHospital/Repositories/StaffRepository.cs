using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Data;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories;

public sealed class StaffRepository : IStaffRepository
{
    private readonly List<Staff> _staffList;
    private readonly DatabaseManager _dbManager;

    public StaffRepository(DatabaseManager dbManager)
    {
        _dbManager = dbManager;
        _staffList = new List<Staff>();
        LoadStaff();
    }

    private void LoadStaff()
    {
        _ = _dbManager.ConnectionFactory;

        if (_staffList.Count > 0)
            return;

        RegisterStaff(new Staff
        {
            Id = 1,
            StaffCode = "PHARM001",
            DisplayName = "Pharmacist",
            Role = "Pharmacist",
            IsAvailable = true
        });

        RegisterStaff(new Staff
        {
            Id = 2,
            StaffCode = "DOC001",
            DisplayName = "Dr. Sample",
            Role = "Doctor",
            Specialization = "Cardiology",
            IsAvailable = true
        });
    }

    private void RegisterStaff(Staff newStaff)
    {
        if (newStaff.Id == 0)
            newStaff.Id = _staffList.Count == 0 ? 1 : _staffList.Max(s => s.Id) + 1;

        if (string.IsNullOrWhiteSpace(newStaff.StaffCode))
            newStaff.StaffCode = $"STAFF{newStaff.Id:D3}";

        _staffList.Add(newStaff);
    }

    private void RemoveStaff(int staffId)
    {
        var idx = _staffList.FindIndex(s => s.Id == staffId);
        if (idx >= 0)
            _staffList.RemoveAt(idx);
    }

    private List<Doctor> GetAvailableDoctors()
    {
        return _staffList
            .Where(s => string.Equals(s.Role, "Doctor", StringComparison.OrdinalIgnoreCase))
            .Select(s => new Doctor
            {
                Id = s.StaffCode,
                Name = s.DisplayName,
                Specialization = s.Specialization
            })
            .ToList();
    }

    public Doctor? GetDoctorBySpecialization(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return null;

        foreach (var doctor in GetAvailableDoctors())
        {
            var staff = _staffList.FirstOrDefault(s =>
                string.Equals(s.StaffCode, doctor.Id, StringComparison.OrdinalIgnoreCase));
            if (staff != null &&
                !string.IsNullOrEmpty(staff.Specialization) &&
                staff.Specialization.Contains(spec, StringComparison.OrdinalIgnoreCase))
                return doctor;
        }
        return null;
    }

    public Staff? FindByStaffCode(string staffCode)
    {
        if (string.IsNullOrWhiteSpace(staffCode))
            return null;

        return _staffList.FirstOrDefault(s =>
            string.Equals(s.StaffCode, staffCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}