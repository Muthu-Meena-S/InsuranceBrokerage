using InsuranceBrokerage.Application.dto.Policies;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InsuranceBrokerage.Application.Interfaces;

public interface IPolicyService
{
    Task<PolicyResponse> CreatePolicyAsync(CreatePolicyRequest request);
    Task<List<PolicyResponse>> GetAllPoliciesAsync();
    Task<PolicyResponse?> GetPolicyByIdAsync(int id);
    Task<PolicyResponse?> UpdatePolicyAsync(int id, UpdatePolicyRequest request);
    Task<bool> DeletePolicyAsync(int id);
}
