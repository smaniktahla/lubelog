//VIN lookup and make/model suggestions for the add/edit vehicle modal. Backed by NHTSA vPIC through /Vehicle/DecodeVin.
function initVehicleLookup() {
    loadMakeSuggestions();
    $("#inputYear, #inputMake").on("change", loadModelSuggestions);
    $("#inputLookupMotorcycle").on("change", function () {
        loadMakeSuggestions();
        loadModelSuggestions();
    });
    $("#inputVin").on("input", function () {
        this.value = this.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
    });
}
function isMotorcycleLookup() {
    return $("#inputLookupMotorcycle").is(":checked");
}
function loadMakeSuggestions() {
    $.getJSON('/Vehicle/GetMakes', { motorcycle: isMotorcycleLookup() }, function (response) {
        if (response.success) {
            fillDatalist("#vehicleMakeList", response.additionalData);
        }
    });
}
function loadModelSuggestions() {
    var make = $("#inputMake").val().trim();
    var year = parseInt($("#inputYear").val());
    if (make == '' || isNaN(year) || year < 1900) {
        fillDatalist("#vehicleModelList", []);
        return;
    }
    $.getJSON('/Vehicle/GetModels', { make: make, year: year, motorcycle: isMotorcycleLookup() }, function (response) {
        fillDatalist("#vehicleModelList", response.success ? response.additionalData : []);
    });
}
function fillDatalist(selector, values) {
    var list = $(selector).empty();
    values.forEach(function (value) {
        list.append($("<option>").attr("value", value));
    });
}
function setVinLookupMessage(text, isError) {
    $("#vinLookupMessage").text(text).toggleClass("text-danger", isError).toggleClass("text-muted", !isError).toggleClass("d-none", text == '');
}
function lookupVin() {
    var vin = $("#inputVin").val().trim().toUpperCase();
    $("#vinLookupReview").addClass("d-none").empty();
    if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(vin)) {
        $("#inputVin").addClass("is-invalid");
        setVinLookupMessage("Enter a 17 character VIN (no I, O or Q).", true);
        return;
    }
    $("#inputVin").removeClass("is-invalid");
    setVinLookupMessage("Looking up VIN...", false);
    $.getJSON('/Vehicle/DecodeVin', { vin: vin }, function (response) {
        if (!response.success) {
            setVinLookupMessage(response.message, true);
            return;
        }
        setVinLookupMessage('', false);
        renderVinReview(response.additionalData);
    }).fail(function () {
        setVinLookupMessage("VIN lookup failed, enter the details manually.", true);
    });
}
//extra fields are matched by name, so only fields the admin has defined can be filled.
function getExtraFieldInput(name) {
    return $(".modal.fade.show").find(".extra-field input").filter(function () {
        return $(this).attr("placeholder") == name;
    }).first();
}
function renderVinReview(decoded) {
    var engine = [];
    if (decoded.displacementCC) { engine.push(decoded.displacementCC + "cc"); }
    if (decoded.cylinders) { engine.push(decoded.cylinders + " cyl"); }
    if (decoded.horsePower) { engine.push(decoded.horsePower + " hp"); }
    var rows = [
        { label: "Year", value: decoded.year ? String(decoded.year) : '', current: $("#inputYear").val(), apply: function (v) { $("#inputYear").val(v); } },
        { label: "Make", value: decoded.make, current: $("#inputMake").val(), apply: function (v) { $("#inputMake").val(v); } },
        { label: "Model", value: decoded.model, current: $("#inputModel").val(), apply: function (v) { $("#inputModel").val(v); } },
        { label: "Fuel Type", value: decoded.fuelType, current: $("#inputFuelType").val(), apply: function (v) { $("#inputFuelType").val(v); } }
    ];
    [["Trim", decoded.trim], ["Engine", engine.join(", ")], ["Body Class", decoded.bodyClass], ["Transmission", decoded.transmission], ["Cooling", decoded.cooling], ["Drive Type", decoded.driveType]].forEach(function (pair) {
        var input = getExtraFieldInput(pair[0]);
        if (input.length > 0 && pair[1]) {
            rows.push({ label: pair[0] + " (extra field)", value: pair[1], current: input.val(), apply: function (v) { input.val(v); } });
        }
    });
    var review = $("#vinLookupReview").empty().removeClass("d-none");
    var title = $("<div class='fw-bold'>").text("Review decoded values" + (decoded.isMotorcycle ? " (motorcycle)" : ""));
    review.append(title);
    decoded.warnings.forEach(function (warning) {
        review.append($("<div class='text-warning small'>").text(warning));
    });
    var table = $("<table class='table table-sm mb-1'>");
    table.append("<thead><tr><th></th><th>Field</th><th>Decoded</th><th>Current</th></tr></thead>");
    var body = $("<tbody>").appendTo(table);
    rows.forEach(function (row, index) {
        if (!row.value) { return; }
        var checkbox = $("<input type='checkbox' class='form-check-input'>").prop("checked", true).attr("data-row", index);
        body.append($("<tr>").append($("<td>").append(checkbox), $("<td>").text(row.label), $("<td>").text(row.value), $("<td class='text-muted'>").text(row.current || '')));
    });
    review.append(table);
    var apply = $("<button type='button' class='btn btn-sm btn-primary me-1'>").text("Apply").on("click", function () {
        review.find("input[type=checkbox]:checked").each(function () {
            var row = rows[parseInt($(this).attr("data-row"))];
            row.apply(row.value);
        });
        $("#inputLookupMotorcycle").prop("checked", decoded.isMotorcycle);
        $("#inputVin").val(decoded.vin);
        loadMakeSuggestions();
        loadModelSuggestions();
        review.addClass("d-none").empty();
    });
    var dismiss = $("<button type='button' class='btn btn-sm btn-secondary'>").text("Dismiss").on("click", function () {
        review.addClass("d-none").empty();
    });
    review.append(apply, dismiss);
}
