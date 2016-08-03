﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using Abp.Web.Models;
using ASBicycle.Entities.Authen;
using ASBicycle.Web.Extension.Fliter;
using ASBicycle.Web.Models.Authen;
using ASBicycle.Web.Models.Common;
using AutoMapper;
using Newtonsoft.Json;

namespace ASBicycle.Web.Controllers.Authen
{
    public class RoleController : ASBicycleControllerBase
    {
        private readonly IRepository<Role> _roleRepository;
        private readonly IRepository<Module> _moduleRepository;
        private readonly IRepository<Permission> _permissionRepository;
        private readonly IRepository<ModulePermission> _modulePermissionRepository; 
        private readonly IRepository<RoleModulePermission> _roleModulePermissionRepository; 

        public RoleController(IRepository<Role> roleRepository, IRepository<Module> moduleRepository, IRepository<Permission> permissionRepository,
            IRepository<ModulePermission> modulePermissionRepository, IRepository<RoleModulePermission> roleModulePermissionRepository)
        {
            _roleRepository = roleRepository;
            _moduleRepository = moduleRepository;
            _permissionRepository = permissionRepository;
            _modulePermissionRepository = modulePermissionRepository;
            _roleModulePermissionRepository = roleModulePermissionRepository;
        }
        // GET: 
        //[AdminLayout]
        public ActionResult Index()
        {
            return RedirectToAction("List");
        }
        [AdminLayout]
        //[AdminPermission(PermissionCustomMode.Enforce)]
        public ActionResult List()
        {
            return View();
        }

        [DontWrapResult, UnitOfWork]
        public virtual ActionResult InitDataTable(DataTableParameter param)
        {

            var query =
                _roleRepository.GetAll().OrderBy(s => s.Id).Skip(param.iDisplayStart).Take(param.iDisplayLength);
            var total = _roleRepository.Count();
            var filterResult = query.Select(t => new RoleModel
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                OrderSort = t.OrderSort,
                Enabled = t.Enabled
            }).ToList();
            int sortId = param.iDisplayStart + 1;
            var result = from t in filterResult
                         select new[]
                             {
                                sortId++.ToString(),
                                t.Name,
                                t.Description,
                                t.OrderSort.ToString(),
                                t.Enabled ? "1":"0",
                                t.Id.ToString()
                            };

            return DataTableJsonResult(param.sEcho, param.iDisplayStart, total, total, result);
        }

        public ActionResult Create()
        {
            var model = new RoleModel();
            //PrepareAllUserModel(model);
            return PartialView(model);
        }

        [HttpPost, UnitOfWork]
        public virtual ActionResult Create(RoleModel model)
        {
            if (ModelState.IsValid)
            {
                Mapper.CreateMap<RoleModel, Role>();
                var user = Mapper.Map<Role>(model);
                user = _roleRepository.Insert(user);

                //SuccessNotification("添加成功");
                return Json(model);
            }
            return Json(null);
        }
        [UnitOfWork]
        public virtual ActionResult Edit(int id)
        {
            Mapper.CreateMap<Role, RoleModel>();
            var model = Mapper.Map<RoleModel>(_roleRepository.Get(id));
            //var model = role.ToModel();
            //PrepareAllUserModel(model);
            return PartialView(model);
        }

        [HttpPost, UnitOfWork]
        public virtual ActionResult Edit(RoleModel model)
        {
            var user = _roleRepository.Get(model.Id);

            if (ModelState.IsValid)
            {
                user.Name = model.Name;
                user.Description = model.Description;
                user.OrderSort = model.OrderSort;
                user.Enabled = model.Enabled;

                user = _roleRepository.Update(user);
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
            _roleRepository.Delete(s => s.Id == id);
            //var role = _roleService.GetRoleById(id);
            //_roleService.DeleteRole(role);

            return Json(new { success = true });
        }

        [UnitOfWork]
        public virtual ActionResult SetPermission(int id)
        {
            //角色 - 菜单
            var model = new RoleSelectedModuleModel();

            #region 角色

            var role = _roleRepository.Get(id);
            model.RoleId = role.Id;
            model.RoleName = role.Name;

            #endregion

            #region 菜单
            //菜单列表
            model.ModuleDataList =
                _moduleRepository.GetAll()
                    .Where(m => m.IsMenu && m.Enabled)
                    .Select(m => new ModuleModel1
                    {
                        ModuleId = m.Id,
                        ParentId = m.ParentId,
                        ModuleName = m.Name,
                        Code = m.Code,
                    })
                    .OrderBy(m => m.Code)
                    .ToList();

            //选中菜单
            var selectdModule =
                _roleModulePermissionRepository.GetAll()
                    .Where(t => t.RoleId == id)
                    .Select(t => t.ModuleId)
                    .Distinct()
                    .ToList();

            foreach (var item in model.ModuleDataList)
            {
                if (selectdModule.Contains(item.ModuleId))
                {
                    item.Selected = true;
                }
            }
            #endregion

            return PartialView(model);
        }

        [UnitOfWork, DontWrapResult]
        public virtual ActionResult GetPermission(int roleId, string selectedModules)
        {
            //选中模块
            var selectedModuleId = new List<int>();

            string[] strSelectedModules = selectedModules.Split(',');
            foreach (var item in strSelectedModules)
            {
                var temp = Convert.ToInt32(item);
                if (!selectedModuleId.Contains(temp))
                    selectedModuleId.Add(temp);
            }

            //权限列表
            var model = new RoleSelectedPermissionModel();
            //table头
            model.HeaderPermissionList =
                _permissionRepository.GetAll()
                .Where(p => p.Enabled)
                .Select(p => new PermissionModel1
                {
                    PermissionId = p.Id,
                    PermissionName = p.Name,
                    OrderSort = p.OrderSort
                }).ToList();

            var selectedModuleList = _moduleRepository.GetAll().Where(m => selectedModuleId.Contains(m.Id)).ToList();

            //模块包含的按钮集合
            var modulePermissionList =
                _modulePermissionRepository.GetAll().Where(t => selectedModuleId.Contains(t.ModuleId)).ToList();
            var selectedModulePermissionList =
                _roleModulePermissionRepository.GetAll()
                    .Where(t => t.RoleId == roleId && selectedModuleId.Contains(t.ModuleId))
                    .ToList();

            foreach (var item in selectedModuleList)
            {
                var modulePermissionModel = new ModulePermissionModel
                {
                    ModuleId = item.Id,
                    ParentId = item.ParentId,
                    ModuleName = item.Name,
                    Code = item.Code
                };

                //所有权限列表
                foreach (var permission in model.HeaderPermissionList)
                {
                    modulePermissionModel.PermissionDataList.Add(new PermissionModel1
                    {
                        PermissionId = permission.PermissionId,
                        PermissionName = permission.PermissionName,
                        OrderSort = permission.OrderSort,
                    });
                }

                var modulePermission = modulePermissionList.Where(m => m.ModuleId == item.Id).ToList();
                var selectedModulePermission = selectedModulePermissionList.Where(m => m.ModuleId == item.Id).ToList();

                if (item.ChildModule.Count > 0 && selectedModulePermission.Any())
                {
                    modulePermissionModel.Selected = true;
                }

                foreach (var mp in modulePermission)
                {
                    var permission = model.HeaderPermissionList.FirstOrDefault(t => t.PermissionId == mp.PermissionId);

                    foreach (var p in modulePermissionModel.PermissionDataList)
                    {
                        if (permission != null && p.PermissionId == permission.PermissionId)
                        {
                            //设置Checkbox可用
                            p.Enabled = true;
                            //设置选中
                            var rmp = selectedModulePermission.FirstOrDefault(t=> t.PermissionId == permission.PermissionId);
                            if (rmp != null)
                            {
                                //设置父节点选中
                                modulePermissionModel.Selected = true;
                                p.Selected = true;
                            }
                        }
                    }

                }
                model.ModulePermissionDataList.Add(modulePermissionModel);
            }
            //权限按照Code排序
            model.ModulePermissionDataList = model.ModulePermissionDataList.OrderBy(t => t.Code).ToList();

            return PartialView(model);
        }

        [UnitOfWork, HttpPost]
        public virtual ActionResult SetPermission(int roleId, string isSet, string newModulePermission)
        {
            if (isSet == "0")
            {
                throw new UserFriendlyException("请选择按钮权限");
            }
            var newModulePermissionList = JsonConvert.DeserializeObject<List<RoleModulePermissionModel>>(newModulePermission);
            
            
            //选中的模块权限
            var oldModulePermissionList =
                _roleModulePermissionRepository.GetAll()
                    .Where(t => t.RoleId == roleId)
                    .Select(t => new RoleModulePermissionModel
                    {
                        RoleId = t.RoleId,
                        ModuleId = t.ModuleId,
                        PermissionId = t.PermissionId
                    }).ToList();
            var sameModulePermissionList = oldModulePermissionList.Intersect(newModulePermissionList);
            var addModulePermissionList = newModulePermissionList.Except(sameModulePermissionList);
            var removeModulePermissionList = oldModulePermissionList.Except(sameModulePermissionList);

            foreach (var item in removeModulePermissionList)
            {
                _roleModulePermissionRepository.Delete(t => t.RoleId == item.RoleId && t.ModuleId == item.ModuleId && t.PermissionId == item.PermissionId);
            }
            //提交删除
            CurrentUnitOfWork.SaveChanges();
            foreach (var item in addModulePermissionList)
            {
                _roleModulePermissionRepository.Insert(new RoleModulePermission
                {
                    RoleId = item.RoleId,
                    PermissionId = item.PermissionId,
                    ModuleId = item.ModuleId
                });
            }
            //提交添加
            CurrentUnitOfWork.SaveChanges();
            return Json(newModulePermissionList);
        }
    }
}