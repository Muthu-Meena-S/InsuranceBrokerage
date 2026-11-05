using System;
using System.Collections.Generic;
using System.Text;

namespace InsuranceBrokerage.Application.Services
{
    public class PolicyService : IPolicyService
{
    private readonly IPolicyRepository _repository;

    public PolicyService(IPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<PolicyResponse> CreatePolicyAsync(CreatePolicyRequest request)
    {
        var policy = new Policy
        {
            QuoteId = request.QuoteId,
            PolicyNumber = request.PolicyNumber,
            StartDateUtc = request.StartDate,
            EndDateUtc = request.EndDate,
            Status = PolicyStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
        var created = await _repository.CreateAsync(policy);
        return MapToResponse(created);
    }

    public async Task<List<PolicyResponse>> GetAllPoliciesAsync()
    {
        var policies = await _repository.GetAllAsync();
        return policies.Select(MapToResponse).ToList();
    }

    public async Task<PolicyResponse?> GetPolicyByIdAsync(int id)
    {
        var policy = await _repository.GetByIdAsync(id);
        return policy == null ? null : MapToResponse(policy);
    }

    public async Task<PolicyResponse?> UpdatePolicyAsync(int id, UpdatePolicyRequest request)
    {
        var policy = await _repository.GetByIdAsync(id);
        if (policy == null) return null;
        if (request.PolicyNumber != null) policy.PolicyNumber = request.PolicyNumber;
        if (request.StartDate != null) policy.StartDateUtc = request.StartDate;
        if (request.EndDate != null) policy.EndDateUtc = request.EndDate;
        var updated = await _repository.UpdateAsync(policy);
        return MapToResponse(updated);
    }

    public async Task<bool> DeletePolicyAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    private static PolicyResponse MapToResponse(Policy policy)
    {
        return new PolicyResponse
        {
            Id = policy.Id,
            QuoteId = policy.QuoteId,
            PolicyNumber = policy.PolicyNumber,
            StartDateUtc = policy.StartDateUtc,
            EndDateUtc = policy.EndDateUtc,
            Status = policy.Status.ToString(),
            CreatedAtUtc = policy.CreatedAtUtc
        };
    }
}
    {
    }
}


