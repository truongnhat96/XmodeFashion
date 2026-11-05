using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Fashion;

public sealed class FashionErrors(ILogger<FashionErrors> log) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, message) = context.Exception switch
        {
            CatalogNotFoundException e => (404, e.Message),
            CatalogValidationException e => (400, e.Message),
            DbUpdateConcurrencyException => (409, "Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại."),
            DbUpdateException => (409, "Không thể lưu vì dữ liệu trùng hoặc đang được sử dụng."),
            _ => (500, "Không thể xử lý yêu cầu. Vui lòng thử lại.")
        };
        if (status == 500) log.LogError(context.Exception, "Fashion request failed");
        context.Result = new ObjectResult(new { message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
