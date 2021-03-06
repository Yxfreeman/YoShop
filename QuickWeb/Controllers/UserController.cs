﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Quick.IService;
using Quick.Models.Dto;
using QuickWeb.Controllers.Common;
using QuickWeb.Extensions.Common;
using QuickWeb.Models.ViewModel;

namespace QuickWeb.Controllers
{
    /// <summary>
    /// 用户管理
    /// </summary>
    public class UserController : AdminBaseController
    {
        /// <summary>
        /// yoshop_user对象业务方法
        /// </summary>
        public Iyoshop_userService UserService { get; set; }

        /// <summary>
        /// 用户列表
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("/user/index")]
        public IActionResult Index(int? page,int? size)
        {
            var total = 0;
            var list = UserService.LoadPageEntities<uint>(page ?? 1, size ?? 15, ref total, l => true, s => s.user_id, true).Mapper<IEnumerable<UserDto>>();
            return View(new UserListViewModel(list, total));
        }


    }
}