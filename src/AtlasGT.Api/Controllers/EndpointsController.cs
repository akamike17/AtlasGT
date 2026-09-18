using System;
using System.Collections.Generic;
using System.Linq;
using AtlasGT.Application;
using Microsoft.AspNetCore.Mvc;
using DomainEndpoint = AtlasGT.Domain.Models.Endpoint;

namespace AtlasGT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EndpointsController : ControllerBase
    {
        private static readonly List<DomainEndpoint> Store = new();
        private static readonly object Lock = new();

        public static void RegisterEndpoint(DomainEndpoint ep)
        {
            lock (Lock) { if (!Store.Any(e => e.Id == ep.Id)) Store.Add(ep); }
        }

        [HttpGet]
        public ActionResult<IReadOnlyList<DomainEndpoint>> GetAll()
        {
            lock (Lock) { return Ok(Store.ToList()); }
        }

        [HttpGet("{id:guid}")]
        public ActionResult<DomainEndpoint> GetById(Guid id)
        {
            lock (Lock)
            {
                var ep = Store.FirstOrDefault(e => e.Id == id);
                if (ep is null) return NotFound();
                return Ok(ep);
            }
        }

        [HttpGet("{id:guid}/diagnose")]
        public ActionResult<object> Diagnose(Guid id, [FromServices] IDoctorService doctor)
        {
            DomainEndpoint? ep;
            lock (Lock) { ep = Store.FirstOrDefault(e => e.Id == id); }
            if (ep is null) return NotFound();
            return Ok(doctor.Diagnose(ep));
        }

        [HttpPost]
        public ActionResult<DomainEndpoint> Create([FromBody] DomainEndpoint model)
        {
            if (model is null) return BadRequest();
            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            lock (Lock) { Store.Add(model); }
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }
    }
}
