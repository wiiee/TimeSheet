﻿namespace Web.API
{
    using Common;
    using Entity.Project;
    using Entity.ValueType;
    using Extension;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Platform.Enum;
    using Platform.Extension;
    using Platform.Util;
    using Service.Extension;
    using Service.Project;
    using Service.User;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Web.Model;

    [Authorize]
    [Route("api/[controller]")]
    public class ProjectController : BaseController
    {
        private static ILogger _logger = LoggerUtil.CreateLogger<ProjectController>();

        // GET: api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public Project Get(string id)
        {
            var dbProject = this.GetService<ProjectService>().Get(id);
            var project = JsonUtil.FromJson<Project>(JsonUtil.ToJson(dbProject));

            return project;
        }

        // POST api/values
        [HttpPost]
        public JsonResult Post([FromBody]Project project)
        {
            var projectService = this.GetService<ProjectService>();
            var userService = this.GetService<UserService>();

            if (project == null || string.IsNullOrEmpty(project.Id))
            {
                return Json(new  { errorMsg = "The data have some problem, please check it." });
            }

            var dbProject = projectService.Get(project.Id);

            if (dbProject == null || string.IsNullOrEmpty(dbProject.Id))
            {
                return Json(new { errorMsg = string.Format("Project({0}) doesn't exist.", project.Id) });
            }

            //ToDo: 不是最新的数据，返回错误，请重新编辑
             if (dbProject.LastUpdate != project.LastUpdate)
            {
                return Json(new { errorMsg = string.Format("{0} updated project, please try it again.", dbProject.LastUpdatedBy) });
            }

            //没有权限
            if (!this.GetService<DepartmentService>().IsBoss(this.GetUserId(), dbProject.OwnerIds)
                && !this.GetService<DepartmentService>().GetUsersByUserIds(dbProject.OwnerIds).Select(o => o.Id).Contains(this.GetUserId()))
            {
                return Json(new { errorMsg = "You don't have right!" });
            }

            try
            {
                if (dbProject.IsPublic) //公共项目更新结束时间
                {
                    project.PlanDateRange.EndDate = project.PublishDate;
                    project.PlanEndDate = project.PublishDate;
                    project.ActualDateRange.EndDate = project.PublishDate;
                }
                else
                {
                    var deleteIds = dbProject.Tasks.Select(o => o.Id).Except(project.Tasks.Select(o => o.Id).ToList()).ToList();

                    if (!deleteIds.IsEmpty())
                    {
                        var timeSheetService = this.GetService<TimeSheetService>();
                        var timeSheets = timeSheetService.Get(o => o.ProjectId == project.Id).ToList();

                        foreach (var timeSheet in timeSheets)
                        {
                            timeSheet.DeleteTasks(deleteIds);
                            timeSheetService.Update(timeSheet);
                        }
                    }
                }

                project.UpdateProject();//update actual date range when mark task done
                projectService.Update(project);

                return Json(new { successMsg = string.Format("Edit project({0}:{1}) successfully!", project.Id, project.Name) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        // PUT api/values/5
        [HttpPut]
        public JsonResult Put([FromBody]Project project)
        {
            project.Name = project.Name.Trim();

            if (project.IsPublic)
            {
                project.OwnerIds = new List<string>(new string[] { this.GetUserId() });
                project.UserIds = new List<string>(new string[] { this.GetUserId() });
            }
            
            try{
                this.GetService<ProjectService>().Create(project);
                return Json(new { successMsg = string.Format("Create project({0}:{1}) successfully!", project.Id, project.Name) });
            }
             catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public JsonResult Delete(string id)
        {
            try
            {
                Project project = this.GetService<ProjectService>().Get(id);
                string projectname = project != null ? project.Name : "";
                this.GetService<ProjectService>().Delete(id);
                this.GetService<TimeSheetService>().Delete(o => o.ProjectId == id);
                return Json(new { successMsg = string.Format("Delete project({0}:{1}) successfully!", id, projectname) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        [Route("IsCp4Valid")]
        [HttpPost]
        public JsonResult IsCp4Valid(string serialNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serialNumber))
                {
                    return Json(new {
                        valid = false,
                        message = "You don't input anything, please re-enter it"
                    });
                }

                var project = this.GetService<ProjectService>().Get().Where(o => o.SerialNumber == serialNumber).FirstOrDefault();
                return Json(new {
                    valid = project == null,
                    message = project == null ? string.Format("{0} is valid", serialNumber) : 
                    string.Format("{0} exist already, see <a target='_blank' href='/Report/ProjectOverview?projectId={1}'>Detail</a>", serialNumber, project.Id)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new
                {
                    valid = false,
                    message = ex.Message
                });
            }
        }

        [Route("IsNameExist")]
        [HttpPost]
        public JsonResult IsNameExist(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return Json(new
                    {
                        valid = false,
                        message = "You don't input anything, please re-enter it"
                    });
                }

                name = name.Replace(" ", "");
                var project = this.GetService<ProjectService>().Get().Where(o => o.Name.Replace(" ", "") == name).FirstOrDefault();
                return Json(new
                {
                    valid = project == null,
                    message = project == null ? string.Format("{0} is valid", name) :
                    string.Format("{0} exist already, see <a target='_blank' href='/Report/ProjectOverview?projectId={1}'>Detail</a>", name, project.Id)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new
                {
                    valid = false,
                    message = ex.Message
                });
            }
        }

        [Route("GetTasks")]
        [HttpPost]
        public JsonResult GetTasks(string groupId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var group = this.GetService<DepartmentService>().GetUserGroupById(groupId);
                var projects = this.GetService<ProjectService>().Get().Where(o => 
                    !o.Tasks.IsEmpty() && 
                    group.OwnerIds.Intersect(o.OwnerIds).Count() > 0 &&
                    o.Status != Status.Done &&
                    o.PlanDateRange.StartDate < endDate &&
                    o.PlanDateRange.EndDate > startDate);

                var result = projects.Select(o => new {
                    ProjectId = o.Id,
                    ProjectName = o.Name,
                    ProjectDescription = o.Description,
                    Tasks = o.Tasks
                }).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return null;
            }
        }

        [Route("GetTaskTemplates")]
        [HttpPost]
        public List<TaskInfo> GetTaskTemplates()
        {
            try
            {
                var groupId = this.GetService<DepartmentService>().GetUserGroupsByUserId(this.GetUserId()).FirstOrDefault().Id;
                var taskTemplate = this.GetService<TaskTemplateService>().Get(groupId);
                return taskTemplate == null ? new List<TaskInfo>() : taskTemplate.Tasks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return null;
            }
        }

        [Route("EditForUser")]
        [HttpPost]
        public JsonResult EditForUser([FromBody]Project project)
        {
            try
            {
                this.GetService<ProjectService>().EditForUser(project.Id, project.Name, project.Comment, project.Description);
                return Json(new { successMsg = string.Format("Edit project({0}:{1}) successfully!", project.Id, project.Name) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        [Route("CloseProject")]
        [HttpPost]
        public JsonResult CloseProject(string projectId)
        {
            try
            {
                Project project = this.GetService<ProjectService>().Get(projectId);
                string projectName = project != null ? project.Name : "";
                this.GetService<ProjectService>().CloseProject(projectId);
                return Json(new { successMsg = string.Format("Close project({0}:{1}) successfully!", projectId, projectName) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        [Route("PostponeProject")]
        [HttpPost]
        public JsonResult PostponeProject(string projectId, PostponeReason postponeReason, string comment, DateTime endDate)
        {
            try
            {
                Project project = this.GetService<ProjectService>().Get(projectId);
                string projectName = project != null ? project.Name : "";
                this.GetService<ProjectService>().PostponeProject(projectId, postponeReason, comment, endDate);
                return Json(new { successMsg = string.Format("PostponeProject project({0}:{1}) successfully!", projectId, projectName) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        [Route("GetProject")]
        [HttpPost]
        public Project GetProject(string projectId)
        {
            return this.GetService<ProjectService>().Get(projectId);
        }

        [Route("GetTaskValues")]
        [HttpPost]
        public Dictionary<int, int> GetTaskValues(string projectId)
        {
            var project = this.GetService<ProjectService>().Get(projectId);

            if (!project.Tasks.IsEmpty())
            {
                return project.Tasks.ToDictionary(o => o.Id, o => o.CalculateValue());
            }

            return null;
        }

        [Route("GetProjectModel")]
        [HttpPost]
        public object GetProjectModel(string projectId)
        {
            try
            {
                var project = this.GetService<ProjectService>().Get(projectId);
                return project.BuildProjectModel(this.GetUserId()).ToRow();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return null;
            }
        }

        [Route("GetProjectModels")]
        [HttpPost]
        public List<object> GetProjectModels(string statusText)
        {
            try
            {
                var leaderIds = this.GetService<DepartmentService>().GetLeaderIdsByUserId(this.GetUserId());
                var projects = this.GetService<ProjectService>().Get().Where(o => o.IsPublic || o.OwnerIds.Intersect(leaderIds).Count() > 0);
                projects = projects.OrderBy(o => o.IsPublic).ThenByDescending(o => o.GetEndDate()).ThenByDescending(o => o.UserIds.Contains(this.GetUserId())).ThenByDescending(o => o.Created).ToList();

                if(!string.IsNullOrEmpty(statusText))
                {
                    try
                    {
                        Status status = EnumUtil.ParseEnum<Status>(statusText);
                        projects = projects.Where(o => o.Status == status).ToList();
                    }
                    catch(Exception)
                    {

                    }
                }

                return projects.Select(o => o.BuildProjectModel(this.GetUserId()).ToRow()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return null;
            }
        }

        [Route("Murmur")]
        [HttpPost]
        public JsonResult Murmur(string projectId, long tick, string userId, string content)
        {
            try
            {
                var result = this.GetService<ProjectService>().Murmur(projectId, tick, userId, content);
                return Json(new {
                    Id = result.ToString(),
                    Time = new DateTime(result)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        
        [Route("DeleteMurmur")]
        [HttpPost]
        public void DeleteMurmur(string projectId, long tick, string userId, string content)
        {
            try
            {
                this.GetService<ProjectService>().DeleteMurmur(projectId, tick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        [Route("GetWorkingHour")]
        [HttpPost]
        public double GetWorkingHour(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                return Math.Round(this.GetService<TimeSheetService>().GetUserHoursByProjectId(userId, startDate, endDate).Sum(o => o.Value));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
                return 0;
            }
        }

        [Route("GetPlanHour")]
        [HttpPost]
        public int GetPlanHour(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var hours = this.GetService<ProjectService>().GetPlanHoursByProject(userId, startDate, endDate);
                return (int)Math.Round(hours.Sum(o => o.Value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return 0;
            }
        }

        [Route("ReviewTasks")]
        [HttpPost]
        public JsonResult ReviewTasks([FromBody]ReviewTaskModel model)
        {
            try
            {
                var projectService = this.GetService<ProjectService>();
                var project = projectService.Get(model.ProjectId);
                var userId = this.GetUserId();

                if (model.Items.Select(o => o.TaskId).Except(project.Tasks.Select(o => o.Id)).Count() == 0)
                {
                    foreach (var item in model.Items)
                    {
                        if (model.IsUserMode)
                        {
                            var task = project.Tasks.Find(o => o.Id == item.TaskId);
                            if (task.Values.ContainsKey(userId))
                            {
                                if (this.GetUserType() != UserType.User)
                                {
                                    task.Values[userId] = item.Value;
                                    task.IsReviewed = item.IsReviewed;
                                }
                                else if(!task.IsReviewed)
                                {
                                    task.Values[userId] = item.Value;
                                }
                            }
                        }
                        else if(this.GetUserType() != UserType.User)
                        {
                            if(model.LastUpdate != project.LastUpdate)
                            {
                                return Json(new { errorMsg = "Proejct has been updated, please try again" });
                            }

                            var task = project.Tasks.Find(o => o.Id == item.TaskId);
                            task.Values = item.Values;
                            task.IsReviewed = item.IsReviewed;
                        }
                    }

                    projectService.Update(model.ProjectId, "Tasks", project.Tasks);

                    return Json(new { successMsg = "Review project successfully" });
                }
                else
                {
                    return Json(new { errorMsg = "Proejct has been updated, please try again" });
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new { errorMsg = ex.Message });
            }
        }

        [Route("GetReviewTasks")]
        [HttpPost]
        public ReviewTaskModel GetReviewTasks(string projectId)
        {
            var project = this.GetService<ProjectService>().Get(projectId);
            var userId = this.GetUserId();
            var userIds = this.GetService<DepartmentService>().GetUserGroupsByUserId(userId).First().UserIds;

            var items = project.Tasks.Where(o => userIds.Contains(o.UserId))
                .OrderBy(o => o.UserId)
                .Select(o => new ReviewTaskItem(o.Id, o.Name, o.UserId, o.Description, o.CodeReview, o.IsReviewed, 
                    o.Values.ContainsKey(userId) ? o.Values[userId] : 0, 
                    this.GetUserType() == UserType.User ? null : o.Values))
                .ToList();

            return new ReviewTaskModel(projectId, project.LastUpdate, true, items);
        }
    }
}
