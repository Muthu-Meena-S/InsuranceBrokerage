using System;
using System.Collections.Generic;
using System.Text;

namespace InsuranceBrokerage.Application.Interfaces
{
    public interface IApplicationService
{
    Task<InsuranceApplicationResponse> CreateApplicationAsync(CreateInsuranceApplicationRequest request);
    Task<List<InsuranceApplicationResponse>> GetAllApplicationsAsync();
    Task<InsuranceApplicationResponse?> GetApplicationByIdAsync(int id);
    Task<InsuranceApplicationResponse?> UpdateApplicationAsync(int id, UpdateInsuranceApplicationRequest request);
    Task<bool> DeleteApplicationAsync(int id);
}
    {
    }
}
