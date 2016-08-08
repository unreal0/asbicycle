﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Mvc;
using Abp.Domain.Uow;
using Abp.Web.Models;
using ASBicycle.Bikesite;
using ASBicycle.School;
using ASBicycle.Web.Extension.Fliter;
using ASBicycle.Web.Helper;
using ASBicycle.Web.Models.Common;
using ASBicycle.Web.Models.School;
using AutoMapper;

namespace ASBicycle.Web.Controllers.School
{
    public class BikesiteController : ASBicycleControllerBase
    {
        private readonly IBikesiteRepository _bikesiteRepository;
        private readonly ISchoolRepository _schoolRepository;

        public BikesiteController(IBikesiteRepository bikesiteRepository, ISchoolRepository schoolRepository)
        {
            _bikesiteRepository = bikesiteRepository;
            _schoolRepository = schoolRepository;
        }

        // GET: Bikesite
        //[AdminLayout]
        public ActionResult Index()
        {
            return RedirectToAction("List");
        }
        [AdminLayout]
        //[AdminPermission(PermissionCustomMode.Enforce)]
        public ActionResult List()
        {
            var model = new BikesiteModel();
            return View(model);
        }

        [DontWrapResult, UnitOfWork]
        public virtual ActionResult InitDataTable(DataTableParameter param)
        {
            var expr = BuildSearchCriteria();
            var temp = _bikesiteRepository.GetAll().Where(expr);
            var query = temp
                    .OrderBy(s => s.Id)
                    .Skip(param.iDisplayStart)
                    .Take(param.iDisplayLength);
            var total = temp.Count();
            var filterResult = query.Select(t => new BikesiteModel
            {
                Id = t.Id,
                Name = t.Name,
                Type = t.Type,
                Description = t.Description,
                Rent_charge = t.Rent_charge,
                Return_charge = t.Return_charge,
                Gps_point = t.Gps_point,
                Radius = t.Radius,
                Bike_count = t.Bike_count,
                Available_count = t.Available_count,
                School_name = t.School.Name
            }).ToList();
            int sortId = param.iDisplayStart + 1;
            var result = from t in filterResult
                         select new[]
                             {
                                sortId++.ToString(),
                                t.Name,
                                t.School_name,
                                t.Type.ToString(),
                                t.Gps_point,
                                t.Radius.ToString(),
                                t.Id.ToString()
                            };

            return DataTableJsonResult(param.sEcho, param.iDisplayStart, total, total, result);
        }

        public ActionResult Create()
        {
            var model = new BikesiteModel();
            PrepareAllBikesiteModel(model);
            return PartialView(model);
        }

        [HttpPost, UnitOfWork]
        public virtual ActionResult Create(BikesiteModel model)
        {
            if (ModelState.IsValid)
            {
                Mapper.CreateMap<BikesiteModel, Entities.Bikesite>();
                var bikesite = Mapper.Map<Entities.Bikesite>(model);
                _bikesiteRepository.Insert(bikesite);

                //SuccessNotification("添加成功");
                return Json(model);
            }
            return Json(null);
        }
        [UnitOfWork]
        public virtual ActionResult Edit(int id)
        {
            Mapper.CreateMap<Entities.Bikesite, BikesiteModel>();
            var model = Mapper.Map<BikesiteModel>(_bikesiteRepository.Get(id));
            //var model = role.ToModel();
            PrepareAllBikesiteModel(model);
            return PartialView(model);
        }

        [HttpPost, UnitOfWork]
        public virtual ActionResult Edit(BikesiteModel model)
        {
            var bikesite = _bikesiteRepository.Get(model.Id);

            if (ModelState.IsValid)
            {
                bikesite.Name = model.Name;
                bikesite.Bike_count = model.Bike_count;
                bikesite.Available_count = model.Available_count;
                bikesite.Gps_point = model.Gps_point;
                bikesite.Type = model.Type;
                bikesite.School_id = model.School_id;
                bikesite.Radius = model.Radius;
                bikesite.Updated_at = DateTime.Now;
                
                bikesite = _bikesiteRepository.Update(bikesite);
                
                
                //role = model.ToEntity(role);
                //_roleService.UpdateRole(role);

                //SuccessNotification("更新成功");
                return Json(model);
            }
            return Json(null);
        }

        [HttpPost, UnitOfWork]
        public virtual ActionResult Delete(int id)
        {
            _bikesiteRepository.Delete(s => s.Id == id);
            //var role = _roleService.GetRoleById(id);
            //_roleService.DeleteRole(role);

            return Json(new { success = true });
        }

        #region 公共方法
        [NonAction, UnitOfWork]
        protected virtual void PrepareAllBikesiteModel(BikesiteModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");
            model.SchoolList.AddRange(
                _schoolRepository.GetAll().Select(b => new SelectListItem {Text = b.Name, Value = b.Id.ToString()}));
            model.TypeList.AddRange(new List<SelectListItem>
            {
                new SelectListItem {Text = "普通", Value = "1"},
                new SelectListItem {Text = "防盗", Value = "2"},
                new SelectListItem {Text = "租车", Value = "3"}
            });
        }

        #region 构建查询表达式
        /// <summary>
        /// 构建查询表达式
        /// </summary>
        /// <returns></returns>
        private Expression<Func<Entities.Bikesite, Boolean>> BuildSearchCriteria()
        {
            DynamicLambda<Entities.Bikesite> bulider = new DynamicLambda<Entities.Bikesite>();
            Expression<Func<Entities.Bikesite, Boolean>> expr = null;
            if (!string.IsNullOrEmpty(Request["Name"]))
            {
                var data = Request["Name"].Trim();
                Expression<Func<Entities.Bikesite, Boolean>> tmp = t => t.Name.Contains(data);
                expr = bulider.BuildQueryAnd(expr, tmp);
            }
            if (!string.IsNullOrEmpty(Request["Type"]) && Request["Type"].Trim() != "0")
            {
                var data = Convert.ToInt32(Request["Type"].Trim());
                Expression<Func<Entities.Bikesite, Boolean>> tmp = t => t.Type == data;
                expr = bulider.BuildQueryAnd(expr, tmp);
            }
            var id = CommonHelper.GetSchoolId();
            Expression<Func<Entities.Bikesite, Boolean>> tmpSolid = t => t.School_id == id;
            expr = bulider.BuildQueryAnd(expr, tmpSolid);

            return expr;
        }
        #endregion
        #endregion
    }
}