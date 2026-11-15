-- Product services stay optional. No network response or advertisement is
-- simulated, and a client ad callback alone never awards server currency.
local Json=require('game.json')
local Progress=require('platform.progress')
local Services={};Services.__index=Services
local function available(service) return service~=nil and service.IsAvailable end
local function path_valid(path)
    return type(path)=='string' and path~='' and path:sub(1,1)~='/' and not path:find(':',1,true) and not path:find('..',1,true) and not path:find('[?#\\]')
end
function Services.new(host,progress,config)
    if config==nil then
        config=Json.decode(host:ReadDataText('data/'..host.CurrentGameId..'/services.json'),{max_bytes=65536,max_depth=8})
    end
    assert(type(config)=='table' and not Json.is_null(config),'Invalid platform service configuration')
    for _,key in ipairs({'leaderboard','teams','cloud_save','cloud_load','ad_reward'}) do
        assert(config[key]==nil or config[key]=='' or path_valid(config[key]),'Service route must be a relative game API path: '..key)
    end
    local self=setmetatable({host=host,progress=progress,config=config,closed=false,pending={},ad_pending=false,invalidated=false},Services)
    local account=host.Services.Account
    if account and account.IsAvailable then
        self.identity=account.UserId
        self.invalidated_callback=function()
            if self.closed then return end
            self.invalidated=true;progress.locked=true;progress.persistent=false;progress.status='账号已切换，请重新进入游戏后继续保存。'
            self:cancel_all()
        end
        account:Invalidated('+',self.invalidated_callback)
        self.detach=function() account:Invalidated('-',self.invalidated_callback) end
        host:TrackCallback(function() if self.detach then self.detach();self.detach=nil end end)
    end
    return self
end
function Services:is_available(kind)
    if self.closed or self.invalidated then return false end
    local service=self.host.Services[kind]
    return available(service)
end
function Services:request(path,body,callback,method,idempotency)
    callback=callback or function() end
    if not path_valid(path) then callback(false,'尚未配置此服务的接口地址');return nil end
    if not self:is_available('Requests') then callback(false,'在线服务暂不可用');return nil end
    local request={method=method or (body and 'POST' or 'GET'),path=path}
    if body~=nil then request.bodyJson=Progress.encode(body) end
    if idempotency then request.idempotencyKey=idempotency end
    local token={done=false};self.pending[token]=true
    local function completed(text)
        if token.done or self.closed or self.invalidated then return end
        token.done=true;self.pending[token]=nil
        local ok,response=pcall(Json.decode,text,{max_bytes=1024*1024,max_depth=48})
        if not ok or type(response)~='table' or Json.is_null(response) then callback(false,'服务器返回的数据格式不正确');return end
        -- SDK ApiResult<T> uses {data,error,meta}, not a fabricated success flag.
        local failure=response.error
        if failure~=nil and not Json.is_null(failure) then
            callback(false,type(failure)=='table' and (failure.message or failure.code) or tostring(failure));return
        end
        if response.data==nil or Json.is_null(response.data) then callback(false,'服务器未返回有效的数据');return end
        callback(true,response.data)
    end
    local ok,id=pcall(function() return self.host.Services.Requests:Send(Progress.encode(request),completed) end)
    if not ok then token.done=true;self.pending[token]=nil;callback(false,'在线请求失败');return nil end
    token.id=id
    return id
end
function Services:leaderboard(callback) return self:request(self.config.leaderboard,nil,callback) end
function Services:teams(callback) return self:request(self.config.teams,nil,callback) end
function Services:upload(callback)
    return self:request(self.config.cloud_save,{version=2,progress=self.progress:snapshot()},callback,'POST',CS.System.Guid.NewGuid():ToString('N'))
end
function Services:download(callback) return self:request(self.config.cloud_load,nil,callback) end
function Services:purchase(product,callback)
    -- The SDK intentionally exposes no arbitrary payment API. A product shell
    -- must implement payment + receipt verification before purchases are enabled.
    if callback then callback(false,'支付服务尚未配置，未扣除金币或发起付款。') end
end
function Services:rewarded(placement,callback)
    callback=callback or function() end
    if self.ad_pending then callback(false,'广告正在处理中');return end
    if not self:is_available('Ads') or not path_valid(self.config.ad_reward) then callback(false,'奖励广告服务尚未配置');return end
    self.ad_pending=true
    local generation=self.ad_generation or 0
    local completed=false
    local function result(text)
        if completed or self.closed or self.invalidated or generation~=(self.ad_generation or 0) then return end
        completed=true;self.ad_pending=false
        local ok,data=pcall(Json.decode,text,{max_bytes=65536,max_depth=16})
        if not ok or type(data)~='table' or Json.is_null(data) then callback(false,'广告返回信息无法读取');return end
        local status=tostring(data.status):lower()
        if self.progress and self.progress.data and (status=='completed' or status=='canceled' or status=='cancelled') then
            require('game.ad_cadence').rewarded_closed(self.progress.data,Progress.now());self.progress:changed()
        end
        if tostring(data.status):lower()~='completed' then callback(false,'广告未完成，未发放奖励');return end
        if data.isTest==true then callback(false,'当前为测试广告，不发放正式奖励');return end
        if type(data.requestId)~='string' or data.requestId=='' then callback(false,'广告缺少可验证的请求编号');return end
        -- The backend must verify the SDK callback/server notification and
        -- return a unique authoritative award. Never trust a client completion.
        self:request(self.config.ad_reward,{requestId=data.requestId,placement=placement},callback,'POST','ad:'..data.requestId)
    end
    local ok=pcall(function() self.host.Services.Ads:Show(placement,'rewarded',result) end)
    if not ok then self.ad_pending=false;completed=true;callback(false,'广告服务调用失败') end
end
function Services:interstitial(placement,callback)
    callback=callback or function()end
    local ads=self.host.Services.Ads
    if self.closed or self.invalidated or self.ad_pending or not available(ads) then callback(false);return end
    local canRead,ready=pcall(function()return ads:IsReady(placement,'interstitial')end)
    if not canRead or not ready then callback(false);return end
    self.ad_pending=true;local generation=self.ad_generation or 0;local completed=false
    local function terminal(text)
        if completed or self.closed or self.invalidated or generation~=(self.ad_generation or 0) then return end
        completed=true;self.ad_pending=false
        local ok,value=pcall(Json.decode,text,{max_bytes=65536,max_depth=8})
        callback(ok and type(value)=='table' and value.status=='completed')
    end
    local ok=pcall(function()ads:Show(placement,'interstitial',terminal)end)
    if not ok and not completed then completed=true;self.ad_pending=false;callback(false)end
end
function Services:cancel_all()
    self.ad_generation=(self.ad_generation or 0)+1;self.ad_pending=false
    local service=self.host and self.host.Services.Requests
    for token in pairs(self.pending) do
        token.done=true
        if token.id and service then pcall(function() service:Cancel(token.id) end) end
        self.pending[token]=nil
    end
end
function Services:dispose()
    if self.disposed then return end
    self.closed=true;self:cancel_all()
    if self.detach then self.detach();self.detach=nil end
    self.host=nil;self.progress=nil
    self.disposed=true
end
return Services
