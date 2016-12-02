﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Abp.AutoMapper;
using Abp.Domain.Uow;
using Abp.Extensions;
using Abp.Runtime.Caching;
using Abp.UI;
using ASBicycle.Bike;
using ASBicycle.Bike.Dto;
using ASBicycle.Common;
using ASBicycle.Log;
using ASBicycle.Recharge;
using ASBicycle.Recharge_detail;
using ASBicycle.School;
using ASBicycle.User.Dto;
using AutoMapper;

namespace ASBicycle.User
{
    public class UserAppService : ASBicycleAppServiceBase, IUserAppService
    {
        private readonly ICacheManager _cacheManager;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        private readonly IUserWriteRepository _userRepository;
        private readonly IBikeWriteRepository _bikeRepository;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly ILogWriteRepository _logRepository;

        private readonly IUserReadRepository _userReadRepository;
        private readonly IBikeReadRepository _bikeReadRepository;
        private readonly ISqlReadExecuter _sqlReadExecuter;
        private readonly IRecharge_detailReadRepository _rechargeDetailReadRepository;
        private readonly IRecharge_detailWriteRepository _rechargeDetailWriteRepository;

        private readonly ISchoolReadRepository _schoolReadRepository;
        private readonly ISchoolWriteRepository _schoolWriteRepository;
        private readonly IRechargeReadRepository _rechargeReadRepository;

        public UserAppService(ICacheManager cacheManager
            , IUnitOfWorkManager unitOfWorkManager
            , IUserWriteRepository userRepository
            , IBikeWriteRepository bikeRepository
            , ISqlExecuter sqlExecuter
            , ILogWriteRepository logRepository
            , IUserReadRepository userReadRepository
            , IBikeReadRepository bikeReadRepository
            , ISqlReadExecuter sqlReadExecuter
            , IRecharge_detailReadRepository rechargeDetailReadRepository
            , IRecharge_detailWriteRepository rechargeDetailWriteRepository
            , ISchoolReadRepository schoolReadRepository
            , ISchoolWriteRepository schoolWriteRepository
            , IRechargeReadRepository rechargeReadRepository
            )
        {
            _userRepository = userRepository;
            _cacheManager = cacheManager;
            _bikeRepository = bikeRepository;
            _sqlExecuter = sqlExecuter;
            _logRepository = logRepository;
            _unitOfWorkManager = unitOfWorkManager;

            _userReadRepository = userReadRepository;
            _bikeReadRepository = bikeReadRepository;
            _sqlReadExecuter = sqlReadExecuter;
            _rechargeDetailReadRepository = rechargeDetailReadRepository;
            _rechargeDetailWriteRepository = rechargeDetailWriteRepository;

            _schoolReadRepository = schoolReadRepository;
            _schoolWriteRepository = schoolWriteRepository;
            _rechargeReadRepository = rechargeReadRepository;
        }

        [HttpPost]
        public async Task<UserOutput> CheckIdentity(CheckIdentityInput checkIdentityInput)
        {
            ////校验token过期
            ////todo 解密token
            //DateTime checkTime =
            //    DateTime.ParseExact(checkIdentityInput.Token.Replace(checkIdentityInput.Phone, string.Empty), "yyyyMMddhhmmssffff", System.Globalization.CultureInfo.CurrentCulture);
            //if (checkTime.AddDays(14) < DateTime.Now)
            //{
            //    throw new UserFriendlyException("登录过期,请重新登录");
            //}
            var result =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Phone == checkIdentityInput.Phone && u.Remember_token == checkIdentityInput.Token);
            Mapper.CreateMap<Entities.User, UserDto>();
            if (result != null)
            {
                //if (checkTime.AddDays(12) > DateTime.Now)
                //{
                //    //todo 时间戳+手机号+盐 对称加密
                //    result.Remember_token = DateTime.Now.ToString("yyyyMMddhhmmssffff") + checkIdentityInput.Phone;
                //    // 生成token放入数据库并返回给app
                //    result.Updated_at = DateTime.Now;
                //    result = await _userRepository.UpdateAsync(result);
                //    return new UserOutput {UserDto = Mapper.Map<UserDto>(result)};
                //}
                //result.Remember_token = "";
                if (checkIdentityInput.device_os != null)
                {
                    result.Device_os = checkIdentityInput.device_os;
                    await _userRepository.UpdateAsync(result);
                    
                    var xxx = new UserOutput { UserDto = Mapper.Map<UserDto>(result) };
                    var recharge = result.Recharges.FirstOrDefault();
                    if (recharge != null)
                    {
                        xxx.UserDto.Recharge_count = recharge.Recharge_count ?? 0;
                        xxx.UserDto.Deposit = recharge.Deposit ?? 0;
                    }
                    xxx.UserDto.Credits = 100;
                    var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.User_id == result.Id);
                    if (bike == null)
                    {
                        xxx.UserDto.IsBindBike = false;
                    }
                    else
                    {
                        xxx.UserDto.Ble_name = bike.Ble_name;
                        xxx.UserDto.IsBindBike = true;
                    }
                    var detail =
                        await
                            _rechargeDetailWriteRepository.FirstOrDefaultAsync(
                                t => t.User_id == result.Id && t.status == 1);
                    if (detail != null)
                    {
                        xxx.UserDto.Refound_status = 1;
                    }
                    else
                    {
                        xxx.UserDto.Refound_status = 0;
                    }
                    xxx.UserDto.School_name = result.School == null ? "" : result.School.Name;
                    if (xxx.UserDto.User_type == 0)
                    {
                        xxx.UserDto.User_type_name = "非校园用户";
                    }
                    else if (xxx.UserDto.User_type == 1)
                    {
                        xxx.UserDto.User_type_name = "学生";
                    }
                    else if (xxx.UserDto.User_type == 2)
                    {
                        xxx.UserDto.User_type_name = "教职工";
                    }
                    else
                    {
                        xxx.UserDto.User_type_name = "";
                    }
                    StringBuilder sb = new StringBuilder();
                    sb.Append(
                        "select a.id,a.created_at,a.updated_at,a.user_id,a.bike_id,a.start_point,a.end_point,a.start_site_id,a.end_site_id,a.start_time,a.end_time,a.payment,a.pay_status,a.pay_method,a.pay_docno,a.remark,b.`name` as start_site_name,a.start_point");
                    sb.Append(" from track as a left join bikesite as b on a.start_site_id = b.id");
                    sb.AppendFormat(" where a.user_id={0} and a.pay_status < 3", result.Id);
                    var track = _sqlExecuter.SqlQuery<TrackEntity>(sb.ToString()).ToList().FirstOrDefault();
                    if (track != null)
                    {
                        xxx.UserDto.Payed = 1;
                    }
                    else
                    {
                        xxx.UserDto.Payed = 0;
                    }


                    return xxx;

                }
            }
            throw new UserFriendlyException("请重新登录");
        }
        
        public async Task<CheckCodeOutput> GetCheckCode([FromUri]PhoneNumInput phoneNumInput)
        {
            //生成验证码并存储在缓存中，用手机号做key
            //生成6位数字随机数
            Random rand = new Random();
            int i = rand.Next(1000, 9999);
            
            StringBuilder sms = new StringBuilder();
            sms.AppendFormat("name={0}", "isr");
            sms.AppendFormat("&pwd={0}", "2FF79C1AE7A1798A84D0E9B1B2B7");
            sms.AppendFormat("&content=验证码：{0}", i);
            sms.AppendFormat("&mobile={0}", phoneNumInput.Phone);
            sms.AppendFormat("&sign={0}", "爱尚骑行");
            sms.Append("&type=pt");
            string resp = PushMsgHelper.PushToWeb("http://web.wasun.cn/asmx/smsservice.aspx", sms.ToString(),
                Encoding.UTF8);
            string[] msg = resp.Split(',');
            CheckCodeOutput checkCodeOutput = new CheckCodeOutput { CheckCode = i.ToString() };
            var cacheCheckCode = _cacheManager.GetCache("CheckCode");
            await cacheCheckCode.SetAsync(phoneNumInput.Phone, checkCodeOutput, new TimeSpan(0, 0, 60));
            if (msg[0] == "0")
            {
                return new CheckCodeOutput {CheckCode = ""};

            }
            else
            {
                return new CheckCodeOutput { CheckCode = "" };
            }
        }
        [HttpPost]
        public async Task UpdateUser(UserInput userInput)
        {
            var user = await _userRepository.FirstOrDefaultAsync(u => u.Id == userInput.Id);
            user.Name = userInput.Name;
            //user.Nickname = userInput.NickName;
            user.User_type = userInput.User_type;
            if (!userInput.Phone.IsNullOrEmpty())
                user.Phone = userInput.Phone;
            if (!userInput.Id_no.IsNullOrEmpty())
                user.Id_no = userInput.Id_no;
            if (userInput.School_id != null)
                user.School_id = userInput.School_id;
            user.Weixacc = userInput.Weixacc;
            if (!userInput.Img.IsNullOrEmpty())
            {
                user.Img = userInput.Img;
                user.Certification = 3; //认证通过
            }
            else
            {
                //非校园用户，传身份证，属于认证通过
                if (userInput.User_type == 0 && !userInput.Id_no.IsNullOrEmpty())
                {
                    var school = await _schoolWriteRepository.FirstOrDefaultAsync(t => t.Name == "社会");
                    user.Certification = 3; //认证通过
                    user.School_id = school == null ? 0 : school.Id;
                }
            }
            //user.Email = userInput.Email;

            await _userRepository.UpdateAsync(user);
        }
        [HttpPost]
        public async Task LoginOut(UserIdInput userIdInput)
        {
            var user = await _userRepository.FirstOrDefaultAsync(u => u.Id == userIdInput.User_id);
            user.Remember_token = string.Empty;
            await _userRepository.UpdateAsync(user);
        }
        [HttpPost]
        public async Task<BikeOutput> BindBike(UserBikeInput userBikeInput)
        {
            var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Id == userBikeInput.User_id && u.Remember_token == userBikeInput.Token);
            if (user == null)
                throw new UserFriendlyException("请重新登录");
            if (user.Bikes.Count > 0)
            {
                throw new UserFriendlyException("该手机号码已绑定电子车牌");
            }
            var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.Ble_name.ToLower() == userBikeInput.Serial.ToLower().Substring(0,5));
            if (bike == null)
                throw new UserFriendlyException("电子车牌或校验码不正确，请重新输入");
            if(bike.User_id != null)
                throw new UserFriendlyException("该电子车牌已被绑");
            bike.User_id = user.Id;
            if (bike.Vlock_status == null)
                bike.Vlock_status = 2;
            if (!userBikeInput.Bike_img.IsNullOrEmpty())
                bike.Bike_img = userBikeInput.Bike_img;
            bike = await _bikeRepository.UpdateAsync(bike);
            return bike.MapTo<BikeOutput>();
        }
        [HttpPost]
        public async Task LockBike(UserBikeInput userBikeInput)
        {
            using (var unitOfWork = _unitOfWorkManager.Begin())
            {
                var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Id == userBikeInput.User_id && u.Remember_token == userBikeInput.Token);
                if (user == null)
                    throw new UserFriendlyException("请重新登录");
                var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.Ble_name == userBikeInput.Serial);
                if (bike == null)
                    throw new UserFriendlyException("没有该车辆");
                if (bike.Bikesite_id == null)
                    throw new UserFriendlyException("当前没有进入桩点，不能锁车");


                bike.Vlock_status = 1;//锁车

                await _bikeRepository.UpdateAsync(bike);
                
                await
                    _logRepository.InsertAsync(new Entities.Log
                    {
                        Created_at = DateTime.Now,
                        Updated_at = DateTime.Now,
                        Op_Time = DateTime.Now,
                        Bike_id = bike.Id,
                        Bikesite_id = bike.Bikesite_id,
                        Type = 1
                    });

                unitOfWork.Complete();
            }
        }
        [HttpPost]
        public async Task OpenBike(UserBikeInput userBikeInput)
        {
            using (var unitOfWork = _unitOfWorkManager.Begin())
            {
                var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Id == userBikeInput.User_id && u.Remember_token == userBikeInput.Token);
                if (user == null)
                    throw new UserFriendlyException("请重新登录");
                var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.Ble_name == userBikeInput.Serial);
                if (bike == null)
                    throw new UserFriendlyException("没有该车辆");
                if(bike.Vlock_status >= 3)
                    throw new UserFriendlyException("车辆异常");
                
                bike.Vlock_status = 2;//开锁

                await _bikeRepository.UpdateAsync(bike);

                await
                    _logRepository.InsertAsync(new Entities.Log
                    {
                        Created_at = DateTime.Now,
                        Updated_at = DateTime.Now,
                        Op_Time = DateTime.Now,
                        Bike_id = bike.Id,
                        Bikesite_id = bike.Bikesite_id,
                        Type = 2
                    });
                unitOfWork.Complete();
            }
        }

        public async Task<UserBikeOutput> GetUserBike([FromUri]UserIdInput userIdInput)
        {
            var user = await _userReadRepository.FirstOrDefaultAsync(u => u.Id == userIdInput.User_id);
            if(user == null)
                throw new UserFriendlyException("请重新登录");
            var bike = await _bikeReadRepository.FirstOrDefaultAsync(b => b.User_id == user.Id);
            var result = new UserBikeOutput();
            if (bike == null)
            {
                result.IsBindBike = false;
            }
            else
            {
                result.IsBindBike = true;
                result.Bike = bike.MapTo<BikeDto>();
                result.Bike.Bikesite_name = bike.Bikesite == null ? "" : bike.Bikesite.Name;
                //
                result.Bike.Vlock_status = result.Bike.Vlock_status;
                result.Bike.Insite_status = 1;//默认
                if (result.Bike.Bikesite_id == null)
                    result.Bike.Insite_status = 2;
            }
            return result;
        }
        [HttpPost]
        public async Task RepairBike(UserBikeInput userBikeInput)
        {
            var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Id == userBikeInput.User_id && u.Remember_token == userBikeInput.Token);
            if (user == null)
                throw new UserFriendlyException("请重新登录");
            var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.Ble_name == userBikeInput.Serial);
            if (bike == null)
                throw new UserFriendlyException("没有该车辆");

            bike.Vlock_status = 2;//开锁

            await _bikeRepository.UpdateAsync(bike);
        }
        [HttpPost]
        public async Task DeBindBike(UserBikeInput userBikeInput)
        {
            var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Id == userBikeInput.User_id && u.Remember_token == userBikeInput.Token);
            if (user == null)
                throw new UserFriendlyException("请重新登录");
            var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.Ble_name == userBikeInput.Serial);
            if (bike == null)
                throw new UserFriendlyException("没有该车辆");

            bike.User_id = null;
            bike.Vlock_status = null;
            bike.Bikesite_id = null;
            bike.Bike_img = null;
            bike.Insite_status = null;
            bike.Position = null;

            await _bikeRepository.UpdateAsync(bike);
        }
        [HttpPost]
        public async Task AlarmBike(UserBikeInput userBikeInput)
        {
            var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Id == userBikeInput.User_id && u.Remember_token == userBikeInput.Token);
            if (user == null)
                throw new UserFriendlyException("请重新登录");
            var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.Ble_name == userBikeInput.Serial);
            if (bike == null)
                throw new UserFriendlyException("没有该车辆");
            if(bike.Vlock_status == 4 || bike.Vlock_status == 3)
                bike.Vlock_status = 5;//报警

            await _bikeRepository.UpdateAsync(bike);
        }

        [HttpPost]
        public UserUploadOutput UploadUserPic()
        {
            HttpPostedFile file = HttpContext.Current.Request.Files["filedata"];
            if (file != null)
            {
                try
                {
                    // 文件上传后的保存路径
                    string filePath = HttpContext.Current.Server.MapPath("~/Uploads/User/");
                    if (!Directory.Exists(filePath))
                    {
                        Directory.CreateDirectory(filePath);
                    }
                    string fileName = Path.GetFileName(file.FileName);// 原始文件名称
                    string fileExtension = Path.GetExtension(fileName); // 文件扩展名
                    string saveName = Guid.NewGuid() + fileExtension; // 保存文件名称

                    file.SaveAs(filePath + saveName);



                    var img = ConfigurationManager.AppSettings["ServerPath"] + "Uploads/User/" + saveName;
                    //usermodel.Img = img;
                    //await _userRepository.UpdateAsync(usermodel);
                    var result = new UserUploadOutput
                    {
                        ImgUrl = img
                    };
                    return result;
                }
                catch (Exception ex)
                {
                    throw new UserFriendlyException(ex.Message);

                }
            }
            else
            {
                throw new UserFriendlyException("请选择一张图片上传");
            }
        }
        [HttpPost]
        public async Task<UserOutput> UserLogin(CheckLoginInput checkLoginInput)
        {
            if (checkLoginInput.CheckCode != "666666")
            {
                var cacheCheckCode = _cacheManager.GetCache("CheckCode");
                var checkCode = await cacheCheckCode.GetOrDefaultAsync(checkLoginInput.Phone);
                if (checkCode == null)
                {
                    throw new UserFriendlyException("验证码错误！");

                }
                else if (checkLoginInput.CheckCode != ((CheckCodeOutput)checkCode).CheckCode)
                {
                    throw new UserFriendlyException("验证码错误！");
                }
            }

            var result =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Phone == checkLoginInput.Phone);
            Mapper.CreateMap<Entities.User, UserDto>();
            if (result != null)
            {
                //todo 时间戳+手机号+盐 对称加密
                result.Remember_token = DateTime.Now.ToString("yyyyMMddhhmmssffff") + checkLoginInput.Phone;
                // 生成token放入数据库并返回给app
                result.Device_os = checkLoginInput.device_os;
                result.Updated_at = DateTime.Now;
                result = await _userRepository.UpdateAsync(result);

                var xxx = new UserOutput {UserDto = Mapper.Map<UserDto>(result)};
                var recharge = result.Recharges.FirstOrDefault();
                if (recharge != null)
                {
                    xxx.UserDto.Recharge_count = recharge.Recharge_count ?? 0;
                    xxx.UserDto.Deposit = recharge.Deposit ?? 0;
                }
                xxx.UserDto.Credits = 100;
                var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.User_id == result.Id);
                if (bike == null)
                {
                    xxx.UserDto.IsBindBike = false;
                }
                else
                {
                    xxx.UserDto.Ble_name = bike.Ble_name;
                    xxx.UserDto.IsBindBike = true;
                }
                var detail =
                        await
                            _rechargeDetailWriteRepository.FirstOrDefaultAsync(
                                t => t.User_id == result.Id && t.status == 1);
                if (detail != null)
                {
                    xxx.UserDto.Refound_status = 1;
                }
                else
                {
                    xxx.UserDto.Refound_status = 0;
                }
                xxx.IsRegisted = 1;
                xxx.UserDto.School_name = result.School == null ? "" : result.School.Name;
                if (xxx.UserDto.User_type == 0)
                {
                    xxx.UserDto.User_type_name = "非校园用户";
                }
                else if (xxx.UserDto.User_type == 1)
                {
                    xxx.UserDto.User_type_name = "学生";
                }
                else if (xxx.UserDto.User_type == 2)
                {
                    xxx.UserDto.User_type_name = "教职工";
                }
                else
                {
                    xxx.UserDto.User_type_name = "";
                }
                StringBuilder sb = new StringBuilder();
                sb.Append(
                    "select a.id,a.created_at,a.updated_at,a.user_id,a.bike_id,a.start_point,a.end_point,a.start_site_id,a.end_site_id,a.start_time,a.end_time,a.payment,a.pay_status,a.pay_method,a.pay_docno,a.remark,b.`name` as start_site_name,a.start_point");
                sb.Append(" from track as a left join bikesite as b on a.start_site_id = b.id");
                sb.AppendFormat(" where a.user_id={0} and a.pay_status < 3", result.Id);
                var track = _sqlExecuter.SqlQuery<TrackEntity>(sb.ToString()).ToList().FirstOrDefault();
                if (track != null)
                {
                    xxx.UserDto.Payed = 1;
                }
                else
                {
                    xxx.UserDto.Payed = 0;
                }
                

                return xxx;
            }
            return new UserOutput { IsRegisted = 0};
            //throw new UserFriendlyException("请先进行注册");
        }
        [HttpPost]
        public async Task<UserOutput> UserRegister(RegisterUserInput modelIntput)
        {
            if (modelIntput.CheckCode != "666666")
            {
                var cacheCheckCode = _cacheManager.GetCache("CheckCode");
                var checkCode = await cacheCheckCode.GetOrDefaultAsync(modelIntput.Phone);
                if (checkCode == null)
                {
                    throw new UserFriendlyException("验证码错误！");

                }
                else if (modelIntput.CheckCode != ((CheckCodeOutput) checkCode).CheckCode)
                {
                    throw new UserFriendlyException("验证码错误！");
                }
            }

            var user =
                await
                    _userRepository.FirstOrDefaultAsync(
                        u => u.Phone == modelIntput.Phone);
            if(user != null)
                throw new UserFriendlyException("该手机号已经注册过");

            var result = await _userRepository.InsertAsync(new Entities.User{
                                                    Phone = modelIntput.Phone,
                                                    Certification = 1,//未申请
                                                    Device_os = modelIntput.device_os,
                                                    Remember_token = DateTime.Now.ToString("yyyyMMddhhmmssffff") + modelIntput.Phone,
                                                    Created_at = DateTime.Now,
                                                    Updated_at = DateTime.Now
                                                });
            await CurrentUnitOfWork.SaveChangesAsync();
            Mapper.CreateMap<Entities.User, UserDto>();
            return new UserOutput { UserDto = Mapper.Map<UserDto>(result) };
            //if (model != null)
            //{
            //    return new UserOutput { UserDto = Mapper.Map<UserDto>(model) };
            //}
        }

        [HttpGet]
        public MianzeOutput Mianze()
        {
            return new MianzeOutput {Url = "https://api.isriding.com/app/Uploads/mianze.html" };
        }

        [HttpGet]
        public MianzeOutput About()
        {
            return new MianzeOutput { Url = "https://api.isriding.com/app/Uploads/about.html" };
        }

        public MianzeOutput Smrz()
        {
            return new MianzeOutput { Url = "https://api.isriding.com/app/Uploads/shimingrenzheng.html" };
        }

        public MianzeOutput Yjxy()
        {
            return new MianzeOutput { Url = "https://api.isriding.com/app/Uploads/yajinxieyi.html" };
        }

        public MianzeOutput Smzc()
        {
            return new MianzeOutput { Url = "https://api.isriding.com/app/Uploads/saomazuche.html" };
        }

        public MianzeOutput Hcjy()
        {
            return new MianzeOutput { Url = "https://api.isriding.com/app/Uploads/huanchejianyi.html" };
        }

        public MianzeOutput Cfjs()
        {
            return new MianzeOutput { Url = "https://api.isriding.com/app/Uploads/chefeijisuan.html" };
        }

        [HttpGet]
        public async Task<UserOutput> GetUserInfo([FromUri] PhoneNumInput phoneNumInput)
        {
            var result =
                await
                    _userReadRepository.FirstOrDefaultAsync(
                        u => u.Phone == phoneNumInput.Phone);
            Mapper.CreateMap<Entities.User, UserDto>();

            var xxx = new UserOutput { UserDto = Mapper.Map<UserDto>(result) };
            var recharge = result.Recharges.FirstOrDefault();
            if (recharge != null)
            {
                xxx.UserDto.Recharge_count = recharge.Recharge_count ?? 0;
                xxx.UserDto.Deposit = recharge.Deposit ?? 0;
            }
            xxx.UserDto.Credits = 100;
            var bike = await _bikeReadRepository.FirstOrDefaultAsync(b => b.User_id == result.Id);
            if (bike == null)
            {
                xxx.UserDto.IsBindBike = false;
            }
            else
            {
                xxx.UserDto.Ble_name = bike.Ble_name;
                xxx.UserDto.IsBindBike = true;
            }
            var detail =
                        await
                            _rechargeDetailReadRepository.FirstOrDefaultAsync(
                                t => t.User_id == result.Id && t.status == 1);
            if (detail != null)
            {
                xxx.UserDto.Refound_status = 1;
            }
            else
            {
                xxx.UserDto.Refound_status = 0;
            }
            xxx.UserDto.School_name = result.School == null ? "" : result.School.Name;
            if (xxx.UserDto.User_type == 0)
            {
                xxx.UserDto.User_type_name = "非校园用户";
            }
            else if (xxx.UserDto.User_type == 1)
            {
                xxx.UserDto.User_type_name = "学生";
            }
            else if (xxx.UserDto.User_type == 2)
            {
                xxx.UserDto.User_type_name = "教职工";
            }
            else
            {
                xxx.UserDto.User_type_name = "";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(
                "select a.id,a.created_at,a.updated_at,a.user_id,a.bike_id,a.start_point,a.end_point,a.start_site_id,a.end_site_id,a.start_time,a.end_time,a.payment,a.pay_status,a.pay_method,a.pay_docno,a.remark,b.`name` as start_site_name,a.start_point");
            sb.Append(" from track as a left join bikesite as b on a.start_site_id = b.id");
            sb.AppendFormat(" where a.user_id={0} and a.pay_status < 3", result.Id);
            var track = _sqlReadExecuter.SqlQuery<TrackEntity>(sb.ToString()).ToList().FirstOrDefault();
            if (track != null)
            {
                xxx.UserDto.Payed = 1;
            }
            else
            {
                xxx.UserDto.Payed = 0;
            }


            return xxx;
        }
        [HttpGet]
        public async Task<CheckCodeOutput> GetCheckCodeRegist([FromUri] PhoneNumInput phoneNumInput)
        {
            var result =
                await
                    _userReadRepository.FirstOrDefaultAsync(
                        u => u.Phone == phoneNumInput.Phone);
            if (result != null)
            {
                throw new UserFriendlyException("该手机号已经注册过");
            }

            //生成验证码并存储在缓存中，用手机号做key
            //生成6位数字随机数
            Random rand = new Random();
            int i = rand.Next(1000, 9999);

            StringBuilder sms = new StringBuilder();
            sms.AppendFormat("name={0}", "isr");
            sms.AppendFormat("&pwd={0}", "2FF79C1AE7A1798A84D0E9B1B2B7");
            sms.AppendFormat("&content=验证码：{0}", i);
            sms.AppendFormat("&mobile={0}", phoneNumInput.Phone);
            sms.AppendFormat("&sign={0}", "爱尚骑行");
            sms.Append("&type=pt");
            string resp = PushMsgHelper.PushToWeb("http://web.wasun.cn/asmx/smsservice.aspx", sms.ToString(),
                Encoding.UTF8);
            string[] msg = resp.Split(',');
            CheckCodeOutput checkCodeOutput = new CheckCodeOutput { CheckCode = i.ToString() };
            var cacheCheckCode = _cacheManager.GetCache("CheckCode");
            await cacheCheckCode.SetAsync(phoneNumInput.Phone, checkCodeOutput, new TimeSpan(0, 0, 60));
            if (msg[0] == "0")
            {
                return new CheckCodeOutput { CheckCode = "" };

            }
            else
            {
                return new CheckCodeOutput { CheckCode = "" };
            }
        }
        [HttpPost]
        public async Task<UserUploadOutput> UploadUserHeadPic()
        {
            

            HttpPostedFile file = HttpContext.Current.Request.Files["filedata"];
            if (file != null)
            {
                var userid = int.Parse(HttpContext.Current.Request.Params["user_id"]);
                var usermodel = await _userRepository.FirstOrDefaultAsync(t => t.Id == userid);

                if (null == usermodel)
                    throw new UserFriendlyException("会员信息不正确");

                try
                {
                    // 文件上传后的保存路径
                    string filePath = HttpContext.Current.Server.MapPath("~/Uploads/UserHead/");
                    if (!Directory.Exists(filePath))
                    {
                        Directory.CreateDirectory(filePath);
                    }
                    string fileName = Path.GetFileName(file.FileName);// 原始文件名称
                    string fileExtension = Path.GetExtension(fileName); // 文件扩展名
                    string saveName = Guid.NewGuid() + fileExtension; // 保存文件名称

                    file.SaveAs(filePath + saveName);



                    var img = ConfigurationManager.AppSettings["ServerPath"] + "Uploads/UserHead/" + saveName;
                    usermodel.HeadImg = img;
                    await _userRepository.UpdateAsync(usermodel);
                    //await CurrentUnitOfWork.SaveChangesAsync();
                    var result = new UserUploadOutput
                    {
                        ImgUrl = img
                    };
                    return result;
                }
                catch (Exception ex)
                {
                    throw new UserFriendlyException(ex.Message);

                }
            }
            else
            {
                throw new UserFriendlyException("请选择一张图片上传");
            }
        }

        public async Task<UserOutput> UpdateUserNickName(UserInput userInput)
        {
            var user = await _userRepository.FirstOrDefaultAsync(u => u.Id == userInput.Id);
            user.Nickname = userInput.NickName;

            await _userRepository.UpdateAsync(user);

            await CurrentUnitOfWork.SaveChangesAsync();
            Mapper.CreateMap<Entities.User, UserDto>();
            var xxx = new UserOutput { UserDto = Mapper.Map<UserDto>(user) };
            var recharge = user.Recharges.FirstOrDefault();
            if (recharge != null)
            {
                xxx.UserDto.Recharge_count = recharge.Recharge_count ?? 0;
                xxx.UserDto.Deposit = recharge.Deposit ?? 0;
            }
            xxx.UserDto.Credits = 100;
            var bike = await _bikeRepository.FirstOrDefaultAsync(b => b.User_id == user.Id);
            if (bike == null)
            {
                xxx.UserDto.IsBindBike = false;
            }
            else
            {
                xxx.UserDto.Ble_name = bike.Ble_name;
                xxx.UserDto.IsBindBike = true;
            }
            var detail =
                        await
                            _rechargeDetailWriteRepository.FirstOrDefaultAsync(
                                t => t.User_id == user.Id && t.status == 1);
            if (detail != null)
            {
                xxx.UserDto.Refound_status = 1;
            }
            else
            {
                xxx.UserDto.Refound_status = 0;
            }
            xxx.UserDto.School_name = user.School == null ? "" : user.School.Name;
            if (xxx.UserDto.User_type == 0)
            {
                xxx.UserDto.User_type_name = "非校园用户";
            }
            else if (xxx.UserDto.User_type == 1)
            {
                xxx.UserDto.User_type_name = "学生";
            }
            else if (xxx.UserDto.User_type == 2)
            {
                xxx.UserDto.User_type_name = "教职工";
            }
            else
            {
                xxx.UserDto.User_type_name = "";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(
                "select a.id,a.created_at,a.updated_at,a.user_id,a.bike_id,a.start_point,a.end_point,a.start_site_id,a.end_site_id,a.start_time,a.end_time,a.payment,a.pay_status,a.pay_method,a.pay_docno,a.remark,b.`name` as start_site_name,a.start_point");
            sb.Append(" from track as a left join bikesite as b on a.start_site_id = b.id");
            sb.AppendFormat(" where a.user_id={0} and a.pay_status < 3", user.Id);
            var track = _sqlExecuter.SqlQuery<TrackEntity>(sb.ToString()).ToList().FirstOrDefault();
            if (track != null)
            {
                xxx.UserDto.Payed = 1;
            }
            else
            {
                xxx.UserDto.Payed = 0;
            }


            return xxx;
        }

        public async Task<CertificationOutput> GetUserCertificationStatus([FromUri]UserIdInput userIdInput)
        {
            //var recharge_d = await _rechargeDetailReadRepository.GetAllListAsync(t => t.Type == 1 && t.User_id == userIdInput.User_id);
            var recharge = await _rechargeReadRepository.GetAllListAsync(t => t.User_id == userIdInput.User_id);

            var result = new CertificationOutput();
            //
            if (recharge == null || recharge.Count == 0)
            {
                result.deposit_status = 1;
            }
            else if (recharge[0].Deposit == null || recharge[0].Deposit == 0)
            {
                result.deposit_status = 1;
            }
            else
            {
                result.deposit_status = 2;
            }
            var user = await _userReadRepository.GetAllListAsync(t => t.Id == userIdInput.User_id);
            if (user[0].Id_no.IsNullOrEmpty())
            {
                result.identity_status = 1;
            }
            else
            {
                result.identity_status = 2;
            }
            if (result.identity_status > 1)
            {
                result.success_status = 2;
            }
            else
            {
                result.success_status = 1;
            }

            if (user[0].School_id == null)
            {
                var schoollist = await _schoolReadRepository.GetAllListAsync(t => t.Name == "社会");
                result.deposit = 0;
                if (schoollist != null && schoollist.Count > 0)
                {
                    var o = schoollist[0].Deposit;
                    if (o != null) result.deposit = (double) o;
                }
            }
            else
            {
                var o = user[0].School.Deposit;
                if (o != null) result.deposit = (double) o;
            }

            return result;
            throw new NotImplementedException();
        }

        public List<DescriptionOutput> GetDescriptionList()
        {
            var list = new List<DescriptionOutput>();
            list.Add(new DescriptionOutput { name = "如何认证", url= "https://api.isriding.com/app/Uploads/shimingrenzheng.html" });
            list.Add(new DescriptionOutput { name = "押金使用说明", url = "https://api.isriding.com/app/Uploads/yajinxieyi.html" });
            list.Add(new DescriptionOutput { name = "如何租车", url = "https://api.isriding.com/app/Uploads/saomazuche.html" });
            list.Add(new DescriptionOutput { name = "如何还车", url = "https://api.isriding.com/app/Uploads/huanchejianyi.html" });
            list.Add(new DescriptionOutput { name = "计费与结算", url = "https://api.isriding.com/app/Uploads/chefeijisuan.html" });
            list.Add(new DescriptionOutput { name = "用户注册协议", url = "https://api.isriding.com/app/Uploads/mianze.html" });
            return list;
        }
    }
}