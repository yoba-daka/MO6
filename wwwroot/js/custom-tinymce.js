(function () {
    angular.module('umbraco').run(['eventsService', function (eventsService) {
        eventsService.on('rteConfigLoaded', function (event, config) {
            config.tinymce.defaultEditorConfig.extended_valid_elements = 'html,head,body';
            config.tinymce.defaultEditorConfig.valid_children = '+body[head|style|meta|link|div|p|h1|h2|h3|h4|h5|h6]';
            config.tinymce.defaultEditorConfig.entity_encoding = 'raw';
            config.tinymce.defaultEditorConfig.cleanup = false;
            config.tinymce.defaultEditorConfig.verify_html = false;
        });
    }]);
})();