//VIN lookup and make/model suggestions for the add/edit vehicle modal. Backed by NHTSA vPIC through /Vehicle/DecodeVin.
function initVehicleLookup() {
    loadMakeSuggestions();
    $("#inputYear, #inputMake").on("change", loadModelSuggestions);
    $("#inputLookupMotorcycle").on("change", function () {
        loadMakeSuggestions();
        loadModelSuggestions();
        refreshSpecFields();
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
function getVehicleSpecs() {
    return $("#vehicleSpecs .spec-field").map(function () {
        return { name: $(this).children("label").text(), value: $(this).find("input").val().trim() };
    }).get().filter(function (spec) { return spec.value != ''; });
}
function buildSpecField(name, value) {
    var field = $("<div class='spec-field'>");
    field.append($("<label>").text(name));
    field.append($("<input type='text' class='form-control'>").attr("placeholder", name).val(value));
    return field;
}
//rebuilds the spec inputs for the selected vehicle kind, keeping every value that has been entered.
function refreshSpecFields(callback) {
    $.getJSON('/Vehicle/GetSpecPreset', { motorcycle: isMotorcycleLookup() }, function (names) {
        var entered = {};
        var extras = [];
        getVehicleSpecs().forEach(function (spec) {
            entered[spec.name] = spec.value;
        });
        var container = $("#vehicleSpecs").empty();
        names.forEach(function (name) {
            container.append(buildSpecField(name, entered[name] || ''));
        });
        Object.keys(entered).filter(function (name) { return names.indexOf(name) < 0; }).forEach(function (name) {
            container.append(buildSpecField(name, entered[name]));
        });
        if (callback) { callback(); }
    });
}
function setSpecValue(name, value) {
    $("#vehicleSpecs .spec-field input").filter(function () { return $(this).attr("placeholder") == name; }).first().val(value);
    $("#collapseSpecs").collapse('show');
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
    [["Trim", decoded.trim], ["Engine", engine.join(", ")], ["Transmission", decoded.transmission], ["Drive Type", decoded.driveType]].forEach(function (pair) {
        if (pair[1]) {
            rows.push({ label: pair[0] + " (spec)", value: pair[1], current: $("#vehicleSpecs .spec-field input").filter(function () { return $(this).attr("placeholder") == pair[0]; }).first().val(), apply: function (v) { setSpecValue(pair[0], v); } });
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
        var checked = review.find("input[type=checkbox]:checked").map(function () {
            return rows[parseInt($(this).attr("data-row"))];
        }).get();
        $("#inputLookupMotorcycle").prop("checked", decoded.isMotorcycle);
        $("#inputVin").val(decoded.vin);
        //spec inputs depend on the vehicle kind, so they are rebuilt before the values are applied.
        refreshSpecFields(function () {
            checked.forEach(function (row) { row.apply(row.value); });
            loadMakeSuggestions();
            loadModelSuggestions();
            review.addClass("d-none").empty();
        });
    });
    var dismiss = $("<button type='button' class='btn btn-sm btn-secondary'>").text("Dismiss").on("click", function () {
        review.addClass("d-none").empty();
    });
    review.append(apply, dismiss);
}
function loadVehicleSpecSummary(vehicleId) {
    $.get(`/Vehicle/GetVehicleSpecSummary?vehicleId=${vehicleId}`, function (data) {
        $("#vehicleSpecSummary").html(data);
    });
}
