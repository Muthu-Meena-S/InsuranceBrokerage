using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Application.dto;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InsuranceBrokerage.Application.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _repository;

    public ApplicationService(IApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<InsuranceApplicationResponse> CreateApplicationAsync(CreateInsuranceApplicationRequest request)
    {
        var app = new InsuranceApplication
        {
            LeadId = request.LeadId,
            InsuranceType = request.InsuranceType,
            CoverageAmount = request.CoverageAmount,
            Currency = request.Currency,
            Description = request.Description,
            Status = ApplicationStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = request.Notes
        };
        var created = await _repository.CreateAsync(app);
        return MapToResponse(created);
    }

    public async Task<List<InsuranceApplicationResponse>> GetAllApplicationsAsync()
    {
        var apps = await _repository.GetAllAsync();
        return apps.Select(MapToResponse).ToList();
    }

    public async Task<InsuranceApplicationResponse?> GetApplicationByIdAsync(int id)
    {
        var app = await _repository.GetByIdAsync(id);
        return app == null ? null : MapToResponse(app);
    }

    public async Task<InsuranceApplicationResponse?> UpdateApplicationAsync(int id, UpdateInsuranceApplicationRequest request)
    {
        var app = await _repository.GetByIdAsync(id);
        if (app == null) return null;
        if (request.InsuranceType != null) app.InsuranceType = request.InsuranceType;
        if (request.CoverageAmount != null) app.CoverageAmount = request.CoverageAmount.Value;
        if (request.Currency != null) app.Currency = request.Currency;
        if (request.Description != null) app.Description = request.Description;
        if (request.Status != null) app.Status = request.Status;
        if (request.Notes != null) app.Notes = request.Notes;
        var updated = await _repository.UpdateAsync(app);
        return MapToResponse(updated);
    }

    public async Task<bool> DeleteApplicationAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    private static InsuranceApplicationResponse MapToResponse(InsuranceApplication app)
    {
        return new InsuranceApplicationResponse
        {
            Id = app.Id,
            LeadId = app.LeadId,
            InsuranceType = app.InsuranceType.ToString(),
            CoverageAmount = app.CoverageAmount,
            Currency = app.Currency,
            Description = app.Description,
            Status = app.Status.ToString(),
            CreatedAtUtc = app.CreatedAtUtc,
            SubmittedAtUtc = app.SubmittedAtUtc,
            Notes = app.Notes
        };
    }
}
